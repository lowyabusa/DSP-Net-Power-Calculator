using DspNetEnergy.Core;
using Microsoft.JSInterop;
using static DspNetEnergy.Web.UiLanguage;
using static DspNetEnergy.Web.Display;

namespace DspNetEnergy.Web;

public partial class App
{
    private readonly Dictionary<string, string> EditorFields = new();
    private MachineEditor? Machine;
    private bool EditingLogistics;
    private string? EditorError;
    private string? EditorInvalid;
    private bool RoutesOpen;
    private RecipeComparison? Routes;
    private int RouteStart;
    private string? RouteStatus;
    private long RouteRevision;

    private void EditMachine(RecipeDefinition recipe, string machine)
    {
        ContextRecipe = null;
        var original = Settings.Steps.GetValueOrDefault(recipe.Id) ?? new ProductionStepSettings();
        var oil = DefaultOilRate(recipe, machine);
        Machine = new(recipe, ProductionCatalog.Machine(machine), original, oil);
        EditorFields["count"] = Edit(original.MachineCount);
        EditorFields["utilization"] = Edit(original.MaximumUtilization * 100m);
        EditorFields["speed"] = Edit(original.RateMultiplier * (oil ?? 1m));
        EditorFields["work"] = Edit(original.WorkingPowerMw);
        EditorFields["idle"] = Edit(original.IdlePowerMw);
        focusDialog = oil.HasValue ? "#edit-speed" : "#edit-count";
    }

    private void EditLogistics()
    {
        EditingLogistics = true;
        SetEquipment("sorters", Settings.Logistics?.Sorters);
        SetEquipment("stations", Settings.Logistics?.Stations);
        SetTrips("drones", Settings.Logistics?.Drones);
        SetTrips("vessels", Settings.Logistics?.Vessels);
        focusDialog = "#edit-sorters-count";
    }

    private void SetEquipment(string prefix, LogisticsEquipment? equipment)
    {
        EditorFields[prefix + "-count"] = Edit(equipment?.Count);
        EditorFields[prefix + "-utilization"] = Edit(equipment?.Utilization * 100m);
        EditorFields[prefix + "-work"] = Edit(equipment?.WorkingKw);
        EditorFields[prefix + "-idle"] = Edit(equipment?.IdleKw);
    }

    private void SetTrips(string prefix, LogisticsTrips? trips)
    {
        EditorFields[prefix + "-rate"] = Edit(trips?.TripsPerMinute);
        EditorFields[prefix + "-energy"] = Edit(trips?.KilojoulesPerTrip);
    }

    private async Task ApplyEditor()
    {
        EditorError = EditorInvalid = null;
        try
        {
            if (Machine is { } machine)
            {
                var updated = machine.Original with
                {
                    MaximumUtilization = Utilization("utilization", optional: false, positive: true),
                    RateMultiplier = EditorDecimal("speed", machine.Oil.HasValue ? T("Crude Oil flow", "Die Rohölförderrate")
                        : T("Throughput multiplier", "Der Durchsatzfaktor"), positive: true)!.Value / (machine.Oil ?? 1m),
                    MachineCount = EditorCount("count", T("Machine count", "Die Maschinenanzahl")),
                    WorkingPowerMw = EditorDecimal("work", T("Working power", "Die Arbeitsleistung"), optional: true),
                    IdlePowerMw = EditorDecimal("idle", T("Idle power", "Die Leerlaufleistung"), optional: true),
                };
                await Dismiss();
                await ChangeStep(machine.Recipe.Id, _ => updated);
            }
            else if (EditingLogistics)
            {
                var updated = new LogisticsSettings(ReadEquipment("sorters"), ReadEquipment("stations"),
                    ReadTrips("drones"), ReadTrips("vessels"));
                await Dismiss();
                await Update(Settings with { Logistics = updated == new LogisticsSettings() ? null : updated });
            }
        }
        catch (InputError invalid)
        {
            EditorError = invalid.Message;
            EditorInvalid = invalid.Field;
            await JS.InvokeVoidAsync("dsp.focus", "#edit-" + invalid.Field);
        }
        catch (OverflowException overflow)
        {
            EditorError = ErrorMessage(overflow);
        }
    }

    private void CloseEditor()
    {
        Machine = null;
        EditingLogistics = false;
        EditorError = EditorInvalid = null;
    }

    private int? EditorCount(string field, string label) => NumericInput.Count(field, EditorFields[field], label);
    private decimal? EditorDecimal(string field, string label, bool optional = false, bool positive = false) =>
        NumericInput.Decimal(field, EditorFields[field], label, optional, positive);

    private decimal Utilization(string field, bool optional, bool positive = false)
    {
        decimal value = EditorDecimal(field, T("Utilization", "Die Auslastung"), optional, positive) ?? 0m;
        if (value > 100m)
            throw new InputError(field, T("Utilization must be at most 100%.", "Die Auslastung darf höchstens 100 % betragen."));
        return value / 100m;
    }

    private LogisticsEquipment? ReadEquipment(string prefix)
    {
        string countField = prefix + "-count";
        int? count = EditorCount(countField, T("Equipment count", "Die Geräteanzahl"));
        if (count is null)
        {
            RequireEmpty(countField, prefix + "-utilization", prefix + "-work", prefix + "-idle");
            return null;
        }
        bool unused = count == 0;
        return new(count.Value, Utilization(prefix + "-utilization", unused),
            EditorDecimal(prefix + "-work", T("Working power", "Die Arbeitsleistung"), optional: unused) ?? 0m,
            EditorDecimal(prefix + "-idle", T("Idle power", "Die Leerlaufleistung"), optional: unused) ?? 0m);
    }

    private LogisticsTrips? ReadTrips(string prefix)
    {
        string rateField = prefix + "-rate";
        decimal? rate = EditorDecimal(rateField, T("Round trips per minute", "Die Umläufe pro Minute"), optional: true);
        if (rate is null)
        {
            RequireEmpty(rateField, prefix + "-energy");
            return null;
        }
        return new(rate.Value, EditorDecimal(prefix + "-energy", T("Energy per round trip", "Die Energie je Umlauf"),
            optional: rate == 0m) ?? 0m);
    }

    private void RequireEmpty(string first, params string[] fields)
    {
        if (fields.Any(field => !string.IsNullOrWhiteSpace(EditorFields[field])))
            throw new InputError(first, T("Enter the first value or leave the entire row blank.", "Gib den ersten Wert ein oder lasse die ganze Zeile leer."));
    }

    private async Task ToggleRoutes()
    {
        RoutesOpen = !RoutesOpen;
        await RefreshRoutes();
    }

    private async Task RefreshRoutes()
    {
        long revision = ++RouteRevision;
        Routes = null;
        RouteStart = 0;
        if (!RoutesOpen) return;
        if (InvalidField is not null)
        {
            RouteStatus = T("Correct the scenario input to compare variants.", "Korrigiere die Szenarioeingabe für den Vergleich.");
            return;
        }

        RouteStatus = T("Calculating recipe chains …", "Rezeptketten werden berechnet …");
        await Task.Delay(30);
        if (revision != RouteRevision) return;
        Routes = RecipeComparison.Compare(Settings.ToScenario(Settings.Tier));
        RouteStatus = null;
    }

    private string RouteSummary(RecipeComparison routes)
    {
        string text = routes.Routes.Count == 0 ? T("No feasible recipe chain.", "Keine berechenbare Rezeptkette.")
            : Format("Chains {0}–{1} of {2}", "Ketten {0}–{1} von {2}", RouteStart + 1, Math.Min(RouteStart + 6, routes.Routes.Count), routes.Routes.Count);
        if (routes.InfeasibleCount > 0)
            text += $" · {routes.InfeasibleCount} {T("infeasible combinations", "Kombinationen nicht berechenbar")}";
        return text;
    }

    private Task ApplyRoute(RecipeRoute route) => Update(Settings with { RecipeChoices = new(route.RecipeChoices) });
    private sealed record MachineEditor(RecipeDefinition Recipe, MachineDefinition Definition, ProductionStepSettings Original, decimal? Oil);
}
