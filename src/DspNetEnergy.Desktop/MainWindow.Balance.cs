using static DspNetEnergy.Desktop.UiLanguage;
using System.Windows;
using System.Windows.Controls;
using DspNetEnergy.Core;

namespace DspNetEnergy.Desktop;

public partial class MainWindow
{
    private void RenderEnergy(ProductionChainBalance? balance)
    {
        CostRows.Children.Clear();
        GenerationRows.Children.Clear();
        EnergyBalance? energy = balance?.Energy;
        NetValue.Text = energy is null ? "—" : Ui.Power(energy.NetPowerMw);
        NetValue.Foreground = Ui.Brush(energy?.NetPowerMw < 0m ? "Error" : "Accent");
        GrossValue.Text = energy is null ? "—" : Ui.Power(energy.GrossPowerMw);
        UseValue.Text = energy is null ? "—" : Ui.Power(energy.TotalConsumptionMw);
        if (balance is null || energy is null)
        {
            BoundaryNote.Text = T("The energy balance appears once the scenario is valid.", "Die Bilanz erscheint nach einer gültigen Berechnung.");
            SprayNote.Text = "—";
            return;
        }

        ConsumptionBreakdown costs = balance.Consumption;
        CostRows.Children.Add(PowerMetric(T("Resource extraction", "Rohstoffgewinnung"), costs.ResourceExtractionMw));
        CostRows.Children.Add(PowerMetric(T("Fuel production", "Brennstoffproduktion"), costs.FuelProductionMw));
        CostRows.Children.Add(PowerMetric(T("Proliferator production", "Proliferator-Herstellung"), costs.ProliferatorProductionMw));
        CostRows.Children.Add(PowerMetric(T("Spraying and logistics", "Sprühtechnik und Logistik"), costs.SprayAndLogisticsMw));
        CostRows.Children.Add(PowerMetric(T("Idle power", "Leerlauf"), costs.IdleMw));

        GenerationRows.Children.Add(Ui.Metric(T("Installed power plants", "Installierte Kraftwerke"), energy.GeneratorCount.ToString(UiLanguage.Culture)));
        GenerationRows.Children.Add(PowerMetric(T("Installed capacity", "Installierte Leistung"), energy.GeneratorCapacityMw));
        GenerationRows.Children.Add(PowerMetric(T("Potential output", "Potenzielle Erzeugung"), energy.PotentialElectricPowerMw));
        GenerationRows.Children.Add(Ui.Metric(T("Energy per item", "Brennwert je Stück"), $"{Ui.Precise(energy.FuelEnergyMjPerItem)} MJ"));
        GenerationRows.Children.Add(Ui.Metric(T("Fuel burned", "Verbrannter Brennstoff"), $"{Ui.Amount(energy.BurnedFuelPerMinute)} /min"));
        if (energy.UnburnedFuelPerMinute > 0m)
        {
            GenerationRows.Children.Add(Ui.Metric(T("Unburned fuel", "Unverbrannter Brennstoff"), $"{Ui.Amount(energy.UnburnedFuelPerMinute)} /min", "Warning"));
        }

        if (balance.ExternalDysonPowerMw > 0m)
        {
            GenerationRows.Children.Add(Ui.Metric(T("External Dyson Sphere power", "Externe Dyson-Energie"), $"{Ui.Power(balance.ExternalDysonPowerMw)} MW", "Blue"));
        }

        var exclusions = new List<string>();
        if (balance.Logistics is LogisticsBalance logistics)
        {
            var logisticsRows = new StackPanel();
            AddLogistics(T("Sorters", "Sorter"), logistics.Sorters);
            AddLogistics(T("Logistics Stations", "Logistikstationen"), logistics.Stations);
            AddLogistics(T("Logistics Drones", "Frachtdrohnen"), logistics.Drones);
            AddLogistics(T("Logistics Vessels", "Logistikschiffe"), logistics.Vessels);
            if (settings.InfrastructureMw is decimal extra)
            {
                logisticsRows.Children.Add(PowerMetric(T("Additional infrastructure", "Zusätzliche Infrastruktur"), extra));
            }
            CostRows.Children.Add(new Expander { Header = T("Logistics cost details", "Logistikkosten im Detail"), Content = logisticsRows,
                Margin = new Thickness(0, 4, 0, 0) });

            void AddLogistics(string label, LogisticsPower? power)
            {
                if (power is null)
                {
                    exclusions.Add(label);
                    return;
                }
                logisticsRows.Children.Add(PowerMetric(label, power.WorkingMw + power.IdleMw));
                if (power.IdleMw > 0m)
                {
                    logisticsRows.Children.Add(PowerMetric(T("↳ of which idle", "↳ davon Leerlauf"), power.IdleMw));
                }
            }
        }
        else if (energy.ExcludedCosts.HasFlag(ExcludedCosts.SortersAndLogistics))
        {
            exclusions.Add(T("Sorters and other logistics", "Sorter und weitere Logistik"));
        }
        else if (settings.InfrastructureMw is decimal infrastructure)
        {
            CostRows.Children.Add(PowerMetric(T("↳ Infrastructure total", "↳ Infrastruktur pauschal"), infrastructure));
        }

        if (energy.ExcludedCosts.HasFlag(ExcludedCosts.SuppliedMaterials))
        {
            string[] supplied = balance.Materials.Where(entry => entry.Value.SuppliedPerMinute > 0m)
                .Select(entry => Inventory.Name(entry.Key)).Order(StringComparer.CurrentCultureIgnoreCase).ToArray();
            exclusions.Add($"{T("Upstream production for", "Vorketten für")} {string.Join(", ", supplied)}");
        }

        BoundaryNote.Text = exclusions.Count == 0 ? T("All configured consumption categories are included.", "Alle konfigurierten Verbrauchskategorien sind enthalten.")
            : $"{T("Excluded", "Ausgeschlossen")}\n• {string.Join("\n• ", exclusions)}";
        BoundaryNote.Foreground = Ui.Brush(exclusions.Count == 0 ? "Muted" : "Warning");
        SprayNote.Text = balance.Spray is SprayBalance spray
            ? $"{spray.UsableSpraysPerUnit} {T("sprays per item", "Sprühvorgänge je Einheit")}\n" +
              $"{Ui.Precise(spray.ProliferatorUnitsPerMinute)} {T("Proliferator items/min", "Proliferator-Einheiten/min")}\n" +
              $"{spray.CoaterCount} {CoaterName(spray.CoaterCount)} · {Ui.Percent(spray.CoaterUtilization)} {T("utilization", "Auslastung")}"
            : T("No Proliferator is active. The balance uses the unsprayed production chain.", "Keine Proliferation aktiv. Der Rechenkern bilanziert die unbesprühte Kette.");
    }

    private static string CoaterName(int count) => count == 1
        ? T("Spray Coater", "Sprühbeschichter") : T("Spray Coaters", "Sprühbeschichter");

    private static Grid PowerMetric(string label, decimal power) => Ui.Metric(label, $"{Ui.Precise(power)} MW");

    private void RenderReferenceChart()
    {
        ReferenceChart.Children.Clear();
        EnergyBalance coal = EnergyCalculator.Calculate(new DirectCombustionScenario(FuelKind.Coal, 120m));
        EnergyBalance graphite = EnergyCalculator.Calculate(new CoalToGraphiteScenario(120m));
        const decimal axisMaximum = 6m;
        ReferenceChart.Children.Add(ChartRow(T("Direct Coal", "Kohle direkt"), coal.NetPowerMw, axisMaximum, "Blue"));
        ReferenceChart.Children.Add(ChartRow(Inventory.Name("energetic-graphite"), graphite.NetPowerMw, axisMaximum, "Accent"));

        var axis = ChartGrid();
        var ticks = new Grid { Margin = new Thickness(0, 3, 0, 2) };
        for (int index = 0; index < 3; index++)
        {
            ticks.ColumnDefinitions.Add(new ColumnDefinition());
            TextBlock tick = Ui.Text($"{index * 2} MW", "Muted", 10);
            Grid.SetColumn(tick, index);
            ticks.Children.Add(tick);
        }

        TextBlock end = Ui.Text("6 MW", "Muted", 10);
        end.HorizontalAlignment = HorizontalAlignment.Right;
        Grid.SetColumn(end, 2);
        ticks.Children.Add(end);
        Grid.SetColumn(ticks, 1);
        axis.Children.Add(ticks);
        ReferenceChart.Children.Add(axis);

        decimal advantage = graphite.NetPowerMw - coal.NetPowerMw;
        var delta = Ui.Text($"{T("Graphite advantage", "Graphitvorteil")}  {advantage.ToString("+0.00;-0.00;0.00", UiLanguage.Culture)} MW",
            "Accent", 13, true);
        delta.Margin = new Thickness(0, 7, 0, 0);
        ReferenceChart.Children.Add(delta);
    }

    private static Grid ChartRow(string title, decimal value, decimal maximum, string brush)
    {
        Grid row = ChartGrid();
        TextBlock label = Ui.Text(title, size: 12, bold: true);
        label.VerticalAlignment = VerticalAlignment.Center;
        row.Children.Add(label);
        var track = new Grid { Height = 13, Margin = new Thickness(0, 4, 0, 4), VerticalAlignment = VerticalAlignment.Center };
        double ratio = (double)Math.Clamp(value / maximum, 0m, 1m);
        track.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(ratio, GridUnitType.Star) });
        track.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1 - ratio, GridUnitType.Star) });
        var background = new Border { Background = Ui.Brush("Outline"), CornerRadius = new CornerRadius(3) };
        Grid.SetColumnSpan(background, 2);
        track.Children.Add(background);
        track.Children.Add(new Border { Background = Ui.Brush(brush), CornerRadius = new CornerRadius(3) });
        Grid.SetColumn(track, 1);
        row.Children.Add(track);
        TextBlock number = Ui.Text($"{Ui.Power(value)} MW", brush, 12, true);
        number.TextAlignment = TextAlignment.Right;
        number.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(number, 2);
        row.Children.Add(number);
        return row;
    }

    private static Grid ChartGrid()
    {
        var row = new Grid { Margin = new Thickness(0, 2, 0, 2) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(92) });
        row.ColumnDefinitions.Add(new ColumnDefinition());
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(76) });
        return row;
    }

    private void RenderVariants(ProductionChainBalance? selectedBalance)
    {
        VariantRows.Children.Clear();
        foreach (ProliferatorTier? tier in Tiers)
        {
            bool active = tier == settings.Tier;
            string name = TierName(tier);
            string? icon = tier is ProliferatorTier level ? ProductionCatalog.Proliferator(level).ItemId : null;
            Button choice = Ui.IconButton(icon, Format("Select {0} in comparison", "{0} im Vergleich auswählen", name), name,
                () => SelectTier(tier), active, 23, "∅");
            var row = new DockPanel { LastChildFill = true, Margin = new Thickness(0, 0, 0, 5) };
            choice.Margin = new Thickness(0, 0, 8, 0);
            row.Children.Add(choice);
            try
            {
                ProductionChainBalance? candidate = active ? selectedBalance : EnergyCalculator.Calculate(settings.ToScenario(tier));
                if (candidate is null)
                {
                    row.Children.Add(Ui.Metric(name, T("Target throughput unavailable", "Zieldurchsatz nicht erreichbar"), "Warning"));
                }
                else
                {
                    string values = PowerComparison(candidate, Ui.Power);
                    row.Children.Add(Ui.Metric(name, values, active ? "Accent" : "Text"));
                }
            }
            catch (Exception error) when (IsScenarioError(error))
            {
                row.Children.Add(Ui.Metric(name, T("Target throughput unavailable", "Zieldurchsatz nicht erreichbar"), "Warning"));
                row.ToolTip = ScenarioMessage(error);
            }

            VariantRows.Children.Add(row);
        }
    }

    private static string PowerComparison(ProductionChainBalance balance, Func<decimal, string> format) =>
        $"{format(balance.Energy.NetPowerMw)} {T("MW net", "MW netto")} · " +
        $"{format(balance.Energy.TotalConsumptionMw)} {T("MW used", "MW Verbrauch")}";
}
