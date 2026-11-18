using static DspNetEnergy.Desktop.UiLanguage;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using DspNetEnergy.Core;

namespace DspNetEnergy.Desktop;

internal sealed class LogisticsEditor : UserControl
{
    internal LogisticsEditor(LogisticsSettings? settings, Action<LogisticsSettings?> finish)
    {
        var content = new StackPanel { Margin = new Thickness(18) };
        content.Children.Add(Ui.Text(T("Logistics costs", "Logistikkosten"), size: 19, bold: true));
        content.Children.Add(Ui.Text(T("Blank rows are excluded. Enter a count or trip rate of 0 to include an unused category explicitly.", "Leere Zeilen bleiben außerhalb der Bilanz. Setze Anzahl oder Fahrten auf 0, um eine ungenutzte Kategorie ausdrücklich zu erfassen."), "Muted"));
        content.Children.Add(Ui.Text(T("Any infrastructure total you entered is added separately. Use it for costs outside these four categories.", "Ein eingetragener Infrastrukturwert wird zusätzlich addiert. Erfasse darin nur Kosten außerhalb dieser vier Kategorien."), "Muted"));
        var sorters = EquipmentFields(T("Sorters", "Sorter"), settings?.Sorters);
        var stations = EquipmentFields(T("Logistics Stations", "Logistikstationen"), settings?.Stations);
        content.Children.Add(Ui.Text(T("Enter station power separately from vehicle charging. Include the return leg in the energy per round trip.", "Stationsleistung ohne Fahrzeugladung angeben. Die Fahrzeugenergie separat je Umlauf einschließlich Rückweg erfassen."), "Muted"));
        var drones = TripFields(T("Logistics Drones", "Frachtdrohnen"), settings?.Drones);
        var vessels = TripFields(T("Logistics Vessels", "Logistikschiffe"), settings?.Vessels);
        var error = Ui.Text("", "Error", 12);
        error.Margin = new Thickness(0, 8, 0, 0);
        content.Children.Add(error);

        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 10, 0, 0) };
        var cancel = new Button { Content = T("Cancel", "Abbrechen"), Margin = new Thickness(0, 0, 6, 0) };
        var apply = new Button { Content = T("Apply", "Übernehmen"), BorderBrush = Ui.Brush("Accent") };
        cancel.Click += (_, _) => finish(null);
        apply.Click += (_, _) => Apply();
        actions.Children.Add(cancel);
        actions.Children.Add(apply);
        content.Children.Add(actions);
        Content = new Border { Style = (Style)Application.Current.FindResource("Panel"), Padding = new Thickness(0),
            BorderBrush = Ui.Brush("Accent"), Child = new ScrollViewer { Content = content,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto } };
        KeyboardNavigation.SetTabNavigation(this, KeyboardNavigationMode.Cycle);
        Loaded += (_, _) => sorters.Count.Focus();
        PreviewKeyDown += (_, args) =>
        {
            if (args.Key == Key.Escape)
            {
                args.Handled = true;
                finish(null);
            }
        };

        void Apply()
        {
            try
            {
                finish(new LogisticsSettings(ReadEquipment(sorters), ReadEquipment(stations), ReadTrips(drones), ReadTrips(vessels)));
            }
            catch (InputError invalid)
            {
                error.Text = invalid.Message;
                invalid.Field.Focus();
            }
        }

        EquipmentInputs EquipmentFields(string label, LogisticsEquipment? equipment)
        {
            UniformGrid row = AddRow(label, 4);
            return new(AddField(row, label, T("Count", "Anzahl"), equipment?.Count),
                AddField(row, label, T("Utilization (%)", "Auslastung (%)"), equipment?.Utilization * 100m),
                AddField(row, label, T("Active (kW/unit)", "Aktiv (kW/Stk.)"), equipment?.WorkingKw),
                AddField(row, label, T("Idle (kW/unit)", "Leerlauf (kW/Stk.)"), equipment?.IdleKw));
        }

        TripInputs TripFields(string label, LogisticsTrips? trips)
        {
            UniformGrid row = AddRow(label, 2);
            return new(AddField(row, label, T("Round trips/min", "Umläufe/min"), trips?.TripsPerMinute),
                AddField(row, label, T("kJ/round trip", "kJ/Umlauf"), trips?.KilojoulesPerTrip));
        }

        UniformGrid AddRow(string label, int columns)
        {
            var heading = Ui.Text(label, size: 13, bold: true);
            heading.Margin = new Thickness(0, 12, 0, 4);
            content.Children.Add(heading);
            var row = new UniformGrid { Columns = columns, Margin = new Thickness(0, 0, 0, 8) };
            content.Children.Add(row);
            return row;
        }
    }

    private static TextBox AddField(Panel row, string category, string label, decimal? value)
    {
        var column = new StackPanel { Margin = new Thickness(0, 0, 5, 0) };
        var field = new TextBox { Text = value is decimal number ? Ui.Edit(number) : "" };
        AutomationProperties.SetName(field, $"{category} · {label}");
        column.Children.Add(Ui.Text(label, "Muted", 11));
        column.Children.Add(field);
        row.Children.Add(column);
        return field;
    }

    private static LogisticsEquipment? ReadEquipment(EquipmentInputs inputs)
    {
        int? count = NumericInput.Count(inputs.Count, T("Equipment count", "Die Geräteanzahl"));
        if (count is null)
        {
            RequireEmptyRow(inputs.Count, inputs.Utilization, inputs.Work, inputs.Idle);
            return null;
        }

        bool unused = count == 0;
        decimal utilization = NumericInput.Decimal(inputs.Utilization, T("Utilization", "Die Auslastung"), optional: unused) ?? 0m;
        if (utilization > 100m)
        {
            throw new InputError(inputs.Utilization, T("Utilization must be at most 100%.", "Die Auslastung darf höchstens 100 % betragen."));
        }
        return new(count.Value, utilization / 100m,
            NumericInput.Decimal(inputs.Work, T("Working power", "Die Arbeitsleistung"), optional: unused) ?? 0m,
            NumericInput.Decimal(inputs.Idle, T("Idle power", "Die Leerlaufleistung"), optional: unused) ?? 0m);
    }

    private static LogisticsTrips? ReadTrips(TripInputs inputs)
    {
        decimal? rate = NumericInput.Decimal(inputs.Rate, T("Round trips per minute", "Die Umläufe pro Minute"), optional: true);
        if (rate is null)
        {
            RequireEmptyRow(inputs.Rate, inputs.Energy);
            return null;
        }

        return new(rate.Value, NumericInput.Decimal(inputs.Energy, T("Energy per round trip", "Die Energie je Umlauf"), optional: rate == 0m) ?? 0m);
    }

    private static void RequireEmptyRow(TextBox first, params TextBox[] rest)
    {
        if (rest.Any(field => !string.IsNullOrWhiteSpace(field.Text)))
        {
            throw new InputError(first, T("Enter the first value or leave the entire row blank.", "Gib den ersten Wert ein oder lasse die ganze Zeile leer."));
        }
    }

    private sealed record EquipmentInputs(TextBox Count, TextBox Utilization, TextBox Work, TextBox Idle);
    private sealed record TripInputs(TextBox Rate, TextBox Energy);
}
