using static DspNetEnergy.Desktop.UiLanguage;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using DspNetEnergy.Core;

namespace DspNetEnergy.Desktop;

public partial class MainWindow : Window
{
    private static readonly ProliferatorTier?[] Tiers = [null, ProliferatorTier.MkI, ProliferatorTier.MkII, ProliferatorTier.MkIII];
    private ScenarioSettings settings = new();
    private ScenarioSettings? lastValidSettings;
    private ProductionChainBalance? lastBalance;
    private FuelKind? lastBalancedFuel;
    private TextBox? invalidField;
    private string? editedStep;
    private IInputElement? inspectorReturnFocus;
    private bool ready;

    private FuelDefinition Fuel => ProductionCatalog.Fuels.Single(fuel => fuel.Kind == settings.Fuel);

    public MainWindow()
    {
        ApplyLanguage(SettingsStore.LoadLanguage());
        InitializeComponent();
        WindowTheme.UseDarkCaption(this);
        RenderLanguageButtons();
        settings = SettingsStore.Load(out string? loadMessage);
        SetInputs();
        RenderReferenceChart();
        ready = true;
        Refresh(rebuildTiles: true);
        if (loadMessage is not null)
        {
            StatusText.Text = loadMessage;
        }
        Loaded += OnGameIconsLoaded;
    }

    private void SetInputs()
    {
        RateBox.Text = Ui.Edit(settings.Rate);
        GeneratorCountBox.Text = settings.GeneratorCount?.ToString(UiLanguage.Culture) ?? "";
        InfrastructureBox.Text = settings.InfrastructureMw is decimal infrastructure ? Ui.Edit(infrastructure) : "";
        CoaterCountBox.Text = settings.CoaterCount?.ToString(UiLanguage.Culture) ?? "";
        CoaterRateBox.Text = Ui.Edit(settings.CoaterRate);
        SprayFuelBox.IsChecked = settings.SprayFuel;
        SelfSprayBox.IsChecked = settings.SelfSpray;
    }

    private void OnSettingsChanged(object sender, RoutedEventArgs args) => Refresh();

    private static void ApplyLanguage(AppLanguage language)
    {
        UiLanguage.Current.Select(language);
        CultureInfo.CurrentCulture = UiLanguage.Culture;
        CultureInfo.CurrentUICulture = UiLanguage.Culture;
    }

    private void RenderLanguageButtons()
    {
        Language = XmlLanguage.GetLanguage(UiLanguage.Culture.IetfLanguageTag);
        AppLanguage language = UiLanguage.Current.Selected;
        Highlight(EnglishButton, language == AppLanguage.English);
        Highlight(GermanButton, language == AppLanguage.German);
        Highlight(ChineseButton, language == AppLanguage.Chinese);

        static void Highlight(Button button, bool selected)
        {
            if (selected)
            {
                button.BorderBrush = Ui.Brush("Accent");
                button.Background = Ui.Brush("Selection");
            }
            else
            {
                button.ClearValue(BorderBrushProperty);
                button.ClearValue(BackgroundProperty);
            }
        }
    }

    private void OnLanguageClick(object sender, RoutedEventArgs args)
    {
        AppLanguage language = ReferenceEquals(sender, ChineseButton) ? AppLanguage.Chinese
            : ReferenceEquals(sender, GermanButton) ? AppLanguage.German : AppLanguage.English;
        if (language == UiLanguage.Current.Selected)
        {
            return;
        }

        CloseInspector();
        ApplyLanguage(language);
        RenderLanguageButtons();
        RenderGameIconStatus();
        ready = false;
        NumericInput.Reformat(RateBox);
        NumericInput.Reformat(InfrastructureBox);
        NumericInput.Reformat(CoaterRateBox);
        ready = true;
        RenderReferenceChart();
        Refresh(rebuildTiles: true);

        try
        {
            SettingsStore.SaveLanguage(language);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            StatusText.Text = T("Could not save the language preference locally.",
                "Die Spracheinstellung konnte lokal nicht gespeichert werden.");
        }
    }

    private void OnClosing(object? sender, CancelEventArgs args)
    {
        routeSearch?.Cancel();
        if (lastValidSettings is null)
        {
            return;
        }

        try
        {
            SettingsStore.Save(lastValidSettings);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, T("Could not save the scenario settings locally.", "Die Szenarioeinstellungen konnten lokal nicht gespeichert werden."), Title,
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void Refresh(bool rebuildTiles = false)
    {
        if (!ready)
        {
            return;
        }

        if (rebuildTiles)
        {
            RenderInventory();
        }

        ScenarioTitle.Text = Inventory.Name(Fuel.ItemId);
        SprayFuelBox.IsEnabled = settings.Tier.HasValue;
        SprayFuelBox.ToolTip = settings.Tier.HasValue ? T("Sprays are counted only for fuel that is burned.", "Nur verbrannter Brennstoff verbraucht Sprühladungen.")
            : T("Select a Proliferator tier first.", "Wähle zuerst eine Proliferator-Stufe.");
        SelfSprayBox.IsEnabled = settings.Tier.HasValue;
        SelfSprayBox.ToolTip = T("One spray coats each unit of Proliferator. The remaining charges are available for other items.", "Eine Ladung besprüht die Proliferator-Einheit selbst. Die übrigen Ladungen stehen für andere Items zur Verfügung.");
        CoaterCountBox.IsEnabled = settings.Tier.HasValue;
        CoaterRateBox.IsEnabled = settings.Tier.HasValue;
        InfrastructureLabel.Text = settings.Logistics is null ? T("Infrastructure total (MW)", "Infrastruktur pauschal (MW)") : T("Additional infrastructure (MW)", "Zusätzliche Infrastruktur (MW)");
        ExportButton.IsEnabled = false;
        invalidField?.ClearValue(Border.BorderBrushProperty);
        invalidField = null;

        try
        {
            settings = ReadInputs();
        }
        catch (InputError error)
        {
            invalidField = error.Field;
            invalidField.BorderBrush = Ui.Brush("Error");
            RenderFailure(error.Message, null);
            VariantRows.Children.Clear();
            VariantRows.Children.Add(Ui.Text(T("Correct the scenario input to compare variants.", "Korrigiere die Szenarioeingabe für den Vergleich."), "Muted"));
            RefreshRouteComparison(null);
            return;
        }

        ProductionScenario scenario = settings.ToScenario(settings.Tier);
        ProductionChainBalance? balance = null;
        try
        {
            balance = EnergyCalculator.Calculate(scenario);
            lastBalance = balance;
            lastBalancedFuel = settings.Fuel;
            lastValidSettings = settings;
            ExportButton.IsEnabled = true;
            ScenarioError.Visibility = Visibility.Collapsed;
            StatusText.Text = T("●  Calculated · steady state", "●  Berechnet · stationärer Betrieb");
            RenderEnergy(balance);
            RenderChain(balance, null);
        }
        catch (Exception error) when (IsScenarioError(error))
        {
            RenderFailure(ScenarioMessage(error), scenario);
        }

        RenderVariants(balance);
        RefreshRouteComparison(scenario);
        editedStep = null;
    }

    private ScenarioSettings ReadInputs() => settings with
    {
        Rate = NumericInput.Decimal(RateBox, T("Target throughput", "Der Zieldurchsatz"))!.Value,
        GeneratorCount = NumericInput.Count(GeneratorCountBox, T("Power plant count", "Die Kraftwerksanzahl")),
        InfrastructureMw = NumericInput.Decimal(InfrastructureBox, T("Infrastructure power", "Die Infrastrukturleistung"), optional: true),
        CoaterCount = settings.Tier.HasValue ? NumericInput.Count(CoaterCountBox, T("Spray Coater count", "Die Coater-Anzahl")) : settings.CoaterCount,
        CoaterRate = settings.Tier.HasValue ? NumericInput.Decimal(CoaterRateBox, T("Spray Coater throughput", "Der Coater-Durchsatz"), positive: true)!.Value : settings.CoaterRate,
        SprayFuel = SprayFuelBox.IsChecked == true,
        SelfSpray = SelfSprayBox.IsChecked == true,
    };

    private void RenderFailure(string message, ProductionScenario? scenario)
    {
        ScenarioError.Text = message;
        ScenarioError.Visibility = Visibility.Visible;
        StatusText.Text = T("●  Waiting for valid scenario input", "●  Bilanz wartet auf eine gültige Einstellung");
        RenderEnergy(null);
        ProductionChainBalance? topology = lastBalancedFuel == settings.Fuel ? lastBalance : null;
        var problemSteps = new HashSet<string>();
        if (scenario is not null)
        {
            if (editedStep is not null)
            {
                problemSteps.Add(editedStep);
            }

            // Solve once with automatic counts to locate an undersized configured step.
            if (scenario.Steps.Values.Any(step => step.MachineCount.HasValue))
            {
                ProductionScenario diagnostic = scenario with
                {
                    Steps = scenario.Steps.ToDictionary(entry => entry.Key, entry => entry.Value with { MachineCount = null }),
                };
                try
                {
                    topology = EnergyCalculator.Calculate(diagnostic);
                    foreach (ProductionStepBalance step in topology.Steps)
                    {
                        if (scenario.Steps.GetValueOrDefault(step.RecipeId)?.MachineCount < step.MachineCount)
                        {
                            problemSteps.Add(step.RecipeId);
                        }
                    }
                }
                catch (Exception error) when (IsScenarioError(error))
                {
                    // Keep the last valid topology available for correction.
                }
            }
        }

        RenderChain(topology, problemSteps);
    }

    private static bool IsScenarioError(Exception error) => error is ArgumentException or InvalidOperationException or OverflowException;

    private static string ScenarioMessage(Exception error) => error switch
    {
        OverflowException => T("The target throughput exceeds the supported range.", "Der Zieldurchsatz überschreitet den berechenbaren Bereich."),
        InvalidOperationException when error.Message.Contains("spray coaters", StringComparison.Ordinal) =>
            T("The installed Spray Coaters cannot handle this throughput.", "Die installierten Spray Coater reichen für diesen Durchsatz nicht aus."),
        InvalidOperationException => T("The selected recipes and facilities cannot meet the target throughput.", "Die gewählten Rezepte und Anlagen können den Zieldurchsatz nicht bereitstellen."),
        _ => Format("Invalid scenario setting. {0}", "Szenarioeinstellung ungültig. {0}", error.Message),
    };

    private void RenderInventory()
    {
        FuelTiles.Children.Clear();
        // Keep the original shortcuts and show a fuel chosen from the full picker alongside them.
        foreach (FuelDefinition fuel in ProductionCatalog.Fuels.Take(5).Append(Fuel).Distinct())
        {
            string label = Inventory.Name(fuel.ItemId);
            Button tile = Ui.IconButton(fuel.ItemId, label, $"{label}\n{FuelEnergy(fuel)}",
                () => SelectFuel(fuel.Kind), settings.Fuel == fuel.Kind, 37, fallbackLabel: label);
            tile.Height = 54;
            tile.Margin = new Thickness(0, 0, 5, 5);
            FuelTiles.Children.Add(tile);
        }

        TierTiles.Children.Clear();
        foreach (ProliferatorTier? tier in Tiers)
        {
            ProliferatorDefinition? rule = tier is ProliferatorTier selected ? ProductionCatalog.Proliferator(selected) : null;
            string title = TierName(tier);
            string tip = rule is null ? T("No Proliferator", "Ohne Proliferation") :
                $"{Inventory.Name(rule.ItemId)}\n{rule.SpraysPerUnit} {T("sprays", "Sprühvorgänge")} · " +
                $"+{Ui.Percent(rule.ExtraProductsMultiplier - 1m)} {T("Extra Products", "Zusatzprodukte")}\n" +
                $"+{Ui.Percent(rule.SpeedMultiplier - 1m)} {T("Production Speedup", "Produktionsbeschleunigung")} · " +
                $"+{Ui.Percent(rule.WorkingPowerMultiplier - 1m)} {T("working power", "Arbeitsleistung")}";
            Button tile = Ui.IconButton(rule?.ItemId, title, tip, () => SelectTier(tier), settings.Tier == tier, 27, "∅");
            var icon = (FrameworkElement)tile.Content;
            tile.Content = null;
            var caption = Ui.Text(title, "Muted", 9, true);
            caption.TextAlignment = TextAlignment.Center;
            var content = new StackPanel();
            content.Children.Add(icon);
            content.Children.Add(caption);
            tile.Content = content;
            tile.Height = 54;
            tile.Margin = new Thickness(0, 0, 4, 0);
            TierTiles.Children.Add(tile);
        }
    }

    private void OnFuelPicker(object sender, RoutedEventArgs args)
    {
        var options = ProductionCatalog.Fuels.Select(fuel => new PickOption(fuel.ItemId, Inventory.Name(fuel.ItemId),
            Inventory.Name(fuel.GeneratorIconId), fuel.ItemId, FuelEnergy(fuel), settings.Fuel == fuel.Kind)).ToArray();
        Pick(T("Select fuel", "Brennstoff wählen"), options, choice => SelectFuel(ProductionCatalog.Fuels.Single(fuel => fuel.ItemId == choice).Kind));
    }

    private static string FuelEnergy(FuelDefinition fuel) =>
        $"{Ui.Precise(fuel.EnergyMj)} {T("MJ per item", "MJ pro Stück")}";

    private void SelectFuel(FuelKind fuel)
    {
        CloseInspector();
        settings = settings with { Fuel = fuel };
        Refresh(rebuildTiles: true);
    }

    private void SelectTier(ProliferatorTier? tier)
    {
        CloseInspector();
        settings = settings with { Tier = tier };
        Refresh(rebuildTiles: true);
    }

    private void Pick(string title, IReadOnlyList<PickOption> options, Action<string> onSelected)
    {
        ShowInspector(new IconPicker(title, options, choice =>
        {
            CloseInspector();
            if (choice is not null)
            {
                onSelected(choice);
            }
        }));
    }

    private void ShowInspector(UserControl inspector)
    {
        inspectorReturnFocus = Keyboard.FocusedElement;
        InspectorHost.Content = inspector;
        InspectorHost.Visibility = Visibility.Visible;
    }

    private void CloseInspector()
    {
        if (InspectorHost.Visibility == Visibility.Visible)
        {
            InspectorHost.Visibility = Visibility.Collapsed;
            InspectorHost.Content = null;
            if (inspectorReturnFocus is not null)
            {
                Keyboard.Focus(inspectorReturnFocus);
            }

            inspectorReturnFocus = null;
        }
    }

    private static string TierName(ProliferatorTier? tier) => tier switch
    {
        ProliferatorTier.MkI => T("Mk.I", "Typ I"),
        ProliferatorTier.MkII => T("Mk.II", "Typ II"),
        ProliferatorTier.MkIII => T("Mk.III", "Typ III"),
        _ => T("None", "Ohne"),
    };
}
