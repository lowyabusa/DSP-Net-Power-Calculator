using static DspNetEnergy.Desktop.UiLanguage;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using DspNetEnergy.Core;

namespace DspNetEnergy.Desktop;

internal sealed class MachineSettingsDialog : UserControl
{
    private readonly ProductionStepSettings original;
    private readonly Action<ProductionStepSettings?> finish;
    private readonly TextBox count;
    private readonly TextBox utilization;
    private readonly TextBox rate;
    private readonly TextBox work;
    private readonly TextBox idle;
    private readonly decimal? defaultOilRate;
    private readonly TextBlock error = Ui.Text("", "Error", 12);
    private TextBox? invalidField;
    internal MachineSettingsDialog(RecipeDefinition recipe, string machineId, ProductionStepSettings settings,
        Action<ProductionStepSettings?> finish)
    {
        original = settings;
        this.finish = finish;
        KeyboardNavigation.SetTabNavigation(this, KeyboardNavigationMode.Cycle);
        var content = new StackPanel { Margin = new Thickness(18) };
        content.Children.Add(Ui.Text(T("MACHINE SETTINGS / ", "MASCHINENPARAMETER / ") + Inventory.Name(recipe.Id), "Accent", 12, true));
        content.Children.Add(Ui.Text(Inventory.Name(machineId), size: 19, bold: true));
        MachineDefinition machine = ProductionCatalog.Machine(machineId);
        count = AddField(T("Machine count · blank = automatic", "Maschinenanzahl · leer = automatisch"), settings.MachineCount);
        utilization = AddField(T("Maximum utilization (%)", "Maximale Auslastung (%)"), settings.MaximumUtilization * 100m);
        defaultOilRate = Ui.DefaultOilRate(recipe, machineId);
        if (defaultOilRate is decimal oilRate)
        {
            rate = AddField(T("Crude Oil per second per extractor at full utilization", "Rohöl pro Sekunde je Extraktor bei voller Auslastung"),
                oilRate * settings.RateMultiplier);
            var hint = Ui.Text(T("Use the current Collecting Speed shown on the Oil Extractor, including Vein Utilization. If your sources differ, enter their average rate.",
                "Verwende die aktuelle Sammelgeschwindigkeit der Ölextraktion einschließlich Adernutzung. Bei unterschiedlich starken Quellen gib den Mittelwert ein."), "Muted", 11);
            hint.Margin = new Thickness(0, 4, 0, 0);
            content.Children.Add(hint);
        }
        else
        {
            rate = AddField(T("Throughput multiplier relative to game value", "Durchsatzfaktor gegenüber Spielwert"), settings.RateMultiplier);
        }
        work = AddField($"{T("Working power (MW) · default", "Arbeitsleistung (MW) · Standard")} {Ui.Precise(machine.WorkingPowerMw)}",
            settings.WorkingPowerMw);
        idle = AddField($"{T("Idle power (MW) · default", "Leerlaufleistung (MW) · Standard")} {Ui.Precise(machine.IdlePowerMw)}",
            settings.IdlePowerMw);
        error.Margin = new Thickness(0, 8, 0, 0);
        content.Children.Add(error);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 10, 0, 0) };
        var cancel = new Button { Content = T("Cancel", "Abbrechen"), Margin = new Thickness(0, 0, 6, 0) };
        cancel.Click += (_, _) => finish(null);
        actions.Children.Add(cancel);
        var apply = new Button { Content = T("Apply", "Übernehmen"), BorderBrush = Ui.Brush("Accent") };
        apply.Click += (_, _) => Apply();
        actions.Children.Add(apply);
        content.Children.Add(actions);
        Content = new Border { Style = (Style)Application.Current.FindResource("Panel"), Padding = new Thickness(0),
            BorderBrush = Ui.Brush("Accent"), Child = new ScrollViewer { Content = content,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto } };
        Loaded += (_, _) => (defaultOilRate.HasValue ? rate : count).Focus();
        PreviewKeyDown += (_, args) =>
        {
            if (args.Key == Key.Escape)
            {
                args.Handled = true;
                finish(null);
            }
            else if (args.Key == Key.Enter && args.OriginalSource is TextBox)
            {
                args.Handled = true;
                Apply();
            }
        };

        TextBox AddField(string label, decimal? value)
        {
            var field = new TextBox { Text = value is decimal number ? Ui.Edit(number) : "" };
            AutomationProperties.SetName(field, label);
            TextBlock caption = Ui.Text(label, "Muted", 12);
            caption.Margin = new Thickness(0, 10, 0, 4);
            content.Children.Add(caption);
            content.Children.Add(field);
            return field;
        }
    }

    private void Apply()
    {
        invalidField?.ClearValue(Border.BorderBrushProperty);
        try
        {
            decimal maximumUtilization = NumericInput.Decimal(utilization, T("Maximum utilization", "Die maximale Auslastung"), positive: true)!.Value;
            if (maximumUtilization > 100m)
            {
                throw new InputError(utilization, T("Utilization must be at most 100%.", "Die Auslastung darf höchstens 100 % betragen."));
            }

            string rateLabel = defaultOilRate.HasValue ? T("Crude Oil flow", "Die Rohölförderrate")
                : T("Throughput multiplier", "Der Durchsatzfaktor");
            decimal enteredRate = NumericInput.Decimal(rate, rateLabel, positive: true)!.Value;
            ProductionStepSettings changed = original with
            {
                MachineCount = NumericInput.Count(count, T("Machine count", "Die Maschinenanzahl")),
                MaximumUtilization = maximumUtilization / 100m,
                RateMultiplier = enteredRate / (defaultOilRate ?? 1m),
                WorkingPowerMw = NumericInput.Decimal(work, T("Working power", "Die Arbeitsleistung"), optional: true),
                IdlePowerMw = NumericInput.Decimal(idle, T("Idle power", "Die Leerlaufleistung"), optional: true),
            };
            finish(changed);
        }
        catch (InputError invalid)
        {
            error.Text = invalid.Message;
            invalidField = invalid.Field;
            invalidField.BorderBrush = Ui.Brush("Error");
            invalidField.Focus();
        }
    }
}
