using static DspNetEnergy.Desktop.UiLanguage;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using DspNetEnergy.Core;
using Microsoft.Win32;

namespace DspNetEnergy.Desktop;

public partial class MainWindow
{
    private static string ScenarioFilter => T("DSP scenario (*.json)|*.json", "DSP-Szenario (*.json)|*.json");
    private CancellationTokenSource? routeSearch;

    private void OnImportScenario(object sender, RoutedEventArgs args)
    {
        var dialog = new OpenFileDialog { Title = T("Import scenario", "Szenario importieren"), Filter = ScenarioFilter };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        ScenarioSettings imported;
        try
        {
            imported = SettingsStore.Read(dialog.FileName);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException || IsScenarioError(error))
        {
            string detail = IsScenarioError(error) ? ScenarioMessage(error) : error.Message;
            MessageBox.Show(this, $"{T("Could not import the scenario.", "Szenario konnte nicht importiert werden.")}\n\n{detail}", Title,
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        CloseInspector();
        ready = false;
        settings = imported;
        SetInputs();
        ready = true;
        Refresh(rebuildTiles: true);
        StatusText.Text = $"{T("Scenario imported", "Szenario importiert")} · {Path.GetFileName(dialog.FileName)}";
    }

    private void OnExportScenario(object sender, RoutedEventArgs args)
    {
        if (lastValidSettings is not ScenarioSettings snapshot || !ExportButton.IsEnabled)
        {
            return;
        }

        var dialog = new SaveFileDialog { Title = T("Export scenario", "Szenario exportieren"), Filter = ScenarioFilter,
            DefaultExt = ".json", FileName = T("dsp-scenario.json", "dsp-szenario.json") };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            SettingsStore.Write(dialog.FileName, snapshot);
            StatusText.Text = $"{T("Scenario exported", "Szenario exportiert")} · {Path.GetFileName(dialog.FileName)}";
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, $"{T("Could not export the scenario.", "Szenario konnte nicht exportiert werden.")}\n\n{error.Message}", Title,
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OnEditLogistics(object sender, RoutedEventArgs args) =>
        ShowInspector(new LogisticsEditor(settings.Logistics, change =>
        {
            CloseInspector();
            if (change is not null)
            {
                settings = settings with { Logistics = change == new LogisticsSettings() ? null : change };
                Refresh();
            }
        }));

    private void OnRouteComparisonChanged(object sender, RoutedEventArgs args)
    {
        if (ready && ReferenceEquals(args.Source, RouteComparisonPanel))
        {
            RefreshRouteComparison(invalidField is null ? settings.ToScenario(settings.Tier) : null);
        }
    }

    private async void RefreshRouteComparison(ProductionScenario? scenario)
    {
        routeSearch?.Cancel();
        routeSearch = null;
        RouteRows.Children.Clear();
        if (!RouteComparisonPanel.IsExpanded)
        {
            return;
        }
        if (scenario is null)
        {
            RouteSummary.Text = T("Correct the scenario input to compare variants.", "Korrigiere die Szenarioeingabe für den Vergleich.");
            return;
        }

        using var search = new CancellationTokenSource();
        routeSearch = search;
        RouteSummary.Text = T("Calculating recipe chains …", "Rezeptketten werden berechnet …");
        try
        {
            RecipeComparison result = await Task.Run(() => RecipeComparison.Compare(scenario, search.Token), search.Token);
            if (!search.IsCancellationRequested)
            {
                RenderRoutePage(result, 0);
            }
        }
        catch (OperationCanceledException) when (search.IsCancellationRequested)
        {
        }
        finally
        {
            if (ReferenceEquals(routeSearch, search))
            {
                routeSearch = null;
            }
        }
    }

    private void RenderRoutePage(RecipeComparison comparison, int first)
    {
        const int pageSize = 6;
        RouteRows.Children.Clear();
        int total = comparison.Routes.Count;
        int end = Math.Min(first + pageSize, total);
        RouteSummary.Text = total == 0 ? T("No feasible recipe chain.", "Keine berechenbare Rezeptkette.")
            : Format("Chains {0}–{1} of {2}", "Ketten {0}–{1} von {2}", first + 1, end, total);
        if (comparison.InfeasibleCount > 0)
        {
            RouteSummary.Text += $" · {comparison.InfeasibleCount} {T("infeasible combinations", "Kombinationen nicht berechenbar")}";
        }

        for (int index = first; index < end; index++)
        {
            RecipeRoute route = comparison.Routes[index];
            string rank = route.IsCurrent ? $"{index + 1}. {T("Active", "Aktiv")}" : $"{index + 1}.";
            var label = Ui.Text($"{rank}  {PowerComparison(route.Balance, Ui.Precise)}",
                route.IsCurrent ? "Accent" : "Text", 12, true);
            var row = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };
            if (!route.IsCurrent)
            {
                var apply = new Button { Content = T("Apply", "Übernehmen"), Margin = new Thickness(8, 0, 0, 0) };
                DockPanel.SetDock(apply, Dock.Right);
                apply.Click += (_, _) =>
                {
                    CloseInspector();
                    settings = settings with { RecipeChoices = new(route.RecipeChoices) };
                    Refresh();
                };
                row.Children.Add(apply);
            }

            var details = new StackPanel();
            details.Children.Add(label);
            string[] recipes = route.Balance.Steps.Select(step => Inventory.Name(step.RecipeId)).ToArray();
            details.Children.Add(Ui.Text(recipes.Length == 0 ? T("No active production steps", "Ohne aktive Produktionsschritte") : string.Join(" · ", recipes), "Muted", 11));
            string[] supplied = route.Balance.Materials.Where(entry => entry.Value.SuppliedPerMinute > 0m)
                .Select(entry => Inventory.Name(entry.Key)).ToArray();
            if (supplied.Length > 0)
            {
                details.Children.Add(Ui.Text($"{T("External supply chains", "Externe Vorketten")} · {string.Join(", ", supplied)}",
                    "Warning", 11));
            }
            row.Children.Add(details);
            RouteRows.Children.Add(row);
        }

        if (total > pageSize)
        {
            var paging = new StackPanel { Orientation = Orientation.Horizontal };
            var previous = new Button { Content = T("Previous", "Zurück"), IsEnabled = first > 0, Margin = new Thickness(0, 0, 6, 0) };
            var next = new Button { Content = T("More chains", "Weitere Ketten"), IsEnabled = end < total };
            previous.Click += (_, _) => RenderRoutePage(comparison, first - pageSize);
            next.Click += (_, _) => RenderRoutePage(comparison, end);
            paging.Children.Add(previous);
            paging.Children.Add(next);
            RouteRows.Children.Add(paging);
        }
    }
}
