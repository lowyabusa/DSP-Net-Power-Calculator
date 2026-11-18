using DspNetEnergy.Core;
using System.Text.Json;

namespace DspNetEnergy.WebParity;

internal static class Scenarios
{
    internal static string Run()
    {
        var scenarios = ProductionCatalog.Fuels.Select(fuel =>
            (ItemId: fuel.ItemId, Scenario: new ProductionScenario(fuel.Kind, 1m))).ToList();

        var graphite = new ProductionScenario(FuelKind.EnergeticGraphite, 60m)
        {
            Proliferation = new(ProliferatorTier.MkIII, SprayFuel: true, SelfSpray: true),
            Steps = new Dictionary<string, ProductionStepSettings>
            {
                ["energetic-graphite"] = new("arc-smelter", MachineCount: 3, MaximumUtilization: .9m, Spray: SprayMode.ExtraProducts),
            },
            Logistics = new(new(6, .75m, 18m, .9m), new(1, .5m, 1500m, 60m), new(3m, 1750m), new(0m, 0m)),
            OtherInfrastructureMw = .04m,
        };
        scenarios.Add(("self-spray-logistics", graphite));
        scenarios.Add(("limited-power-plants", graphite with { GeneratorCount = 0 }));
        scenarios.Add(("external-coal", graphite with { SuppliedItems = new HashSet<string> { "coal" } }));
        scenarios.Add(("undersized-machines", graphite with
        {
            Steps = new Dictionary<string, ProductionStepSettings> { ["energetic-graphite"] = new(MachineCount: 0) },
        }));
        scenarios.Add(("undersized-coaters", graphite with { Proliferation = graphite.Proliferation! with { CoaterCount = 0 } }));
        scenarios.Add(("hydrogen-alternatives", new ProductionScenario(FuelKind.Hydrogen, 20m)));

        var results = scenarios.Select(entry => new
        {
            entry.ItemId,
            Balance = Solve(entry.Scenario),
            Routes = entry.ItemId is "hydrogen-alternatives" or "self-spray-logistics" ? RecipeComparison.Compare(entry.Scenario) : null,
        }).ToArray();
        return JsonSerializer.Serialize(results);
    }

    private static object Solve(ProductionScenario scenario)
    {
        try { return EnergyCalculator.Calculate(scenario); }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or OverflowException)
        {
            return new { Error = error.GetType().Name };
        }
    }
}
