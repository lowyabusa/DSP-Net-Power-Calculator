using DspNetEnergy.Core;
using Xunit;

namespace DspNetEnergy.Tests;

public sealed class PhaseFourAcceptanceTests
{
    [Fact]
    public void LogisticsSeparatesWorkingPowerFromIdlePowerAndMarksMissingCosts()
    {
        var scenario = new ProductionScenario(FuelKind.Coal, 120m) { SuppliedItems = new HashSet<string> { "coal" } };
        ProductionChainBalance result = EnergyCalculator.Calculate(scenario with
        {
            Logistics = new(Sorters: new(10, 0.5m, 20m, 2m), Stations: new(2, 0.25m, 100m, 20m),
                Drones: new(30m, 200m)),
            OtherInfrastructureMw = 0.05m,
        });

        LogisticsBalance logistics = Assert.IsType<LogisticsBalance>(result.Logistics);
        Assert.Equal(new LogisticsPower(0.10m, 0.01m), logistics.Sorters);
        Assert.Equal(new LogisticsPower(0.05m, 0.03m), logistics.Stations);
        Assert.Equal(new LogisticsPower(0.10m, 0m), logistics.Drones);
        Assert.Null(logistics.Vessels);
        Assert.Equal(0.30m, result.Consumption.SprayAndLogisticsMw);
        Assert.Equal(0.04m, result.Consumption.IdleMw);
        Assert.Equal(3.98m, result.Energy.NetPowerMw);
        Assert.True(result.Energy.ExcludedCosts.HasFlag(ExcludedCosts.SortersAndLogistics));

        ProductionChainBalance complete = EnergyCalculator.Calculate(scenario with
        {
            Logistics = new(new(0, 0m, 0m, 0m), new(0, 0m, 0m, 0m), new(0m, 0m), new(3m, 1200m)),
        });
        Assert.Equal(0.06m, complete.Consumption.SprayAndLogisticsMw);
        Assert.Equal(ExcludedCosts.SuppliedMaterials, complete.Energy.ExcludedCosts);
    }

    [Fact]
    public void RecipeComparisonFindsUpstreamAlternativesIntroducedByAlternateRecipes()
    {
        var scenario = new ProductionScenario(FuelKind.EnergeticGraphite, 30m, GeneratorCount: 1)
        {
            OtherInfrastructureMw = 0.05m,
        };
        RecipeComparison comparison = RecipeComparison.Compare(scenario);

        Assert.Equal(EnergyCalculator.Calculate(scenario).Energy, Assert.Single(comparison.Routes, route => route.IsCurrent).Balance.Energy);
        Assert.Contains(comparison.Routes, route => route.Balance.Steps.Any(step => step.RecipeId == "x-ray-cracking")
            && route.Balance.Steps.Any(step => step.RecipeId == "reforming-refine"));
        Assert.Equal(comparison.Routes.OrderByDescending(route => route.Balance.Energy.NetPowerMw), comparison.Routes);
        Assert.All(comparison.Routes, route =>
        {
            Assert.Equal(30m, route.Balance.Energy.AvailableFuelPerMinute);
            Assert.Equal(1, route.Balance.Energy.GeneratorCount);
            Assert.Equal(0.05m, route.Balance.Consumption.SprayAndLogisticsMw);
            Assert.Equal(route.Balance.Energy,
                EnergyCalculator.Calculate(scenario with { RecipeChoices = route.RecipeChoices }).Energy);
        });
    }

    [Fact]
    public void InfeasibleCurrentRecipeDoesNotHideFeasibleAlternatives()
    {
        var scenario = new ProductionScenario(FuelKind.EnergeticGraphite, 30m)
        {
            Steps = new Dictionary<string, ProductionStepSettings> { ["energetic-graphite"] = new(MachineCount: 0) },
        };
        RecipeComparison comparison = RecipeComparison.Compare(scenario);

        Assert.True(comparison.InfeasibleCount > 0);
        Assert.NotEmpty(comparison.Routes);
        Assert.DoesNotContain(comparison.Routes, route => route.IsCurrent);
        Assert.All(comparison.Routes, route => Assert.Equal("x-ray-cracking", route.RecipeChoices["energetic-graphite"]));
    }
}
