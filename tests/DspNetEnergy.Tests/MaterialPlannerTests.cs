using DspNetEnergy.Core;
using Xunit;

namespace DspNetEnergy.Tests;

public sealed class MaterialPlannerTests
{
    [Fact]
    public void ChoosesLowerActivityPlanEvenWhenSkippingTheCoproductRecipeIsFeasible()
    {
        var mainRecipe = Activity("x", "a", new(), new() { ["a"] = 2m, ["b"] = 10m });
        var requiredRecipe = Activity("y", "c", new(), new() { ["a"] = 0.5m, ["c"] = 1m });
        var otherB = Activity("z", "b", new(), new() { ["b"] = 1m });
        var fuel = Activity("f", "fuel", new() { ["a"] = 1m, ["b"] = 10m, ["c"] = 1m }, new() { ["fuel"] = 1m });
        var otherA = Activity("w", "a", new(), new() { ["a"] = 1m });
        var planner = new MaterialPlanner([mainRecipe, requiredRecipe, otherB, fuel, otherA], "fuel", null);

        decimal[] rates = planner.Plan(1m, 0m, 0);

        // Skipping x needs 0.5 cycles of w and 10 of z (total 12.5).
        // Using x just enough to balance a supplies 2.5 b for a total of 9.75 cycles.
        Assert.Equal(new[] { 0.25m, 1m, 7.5m, 1m, 0m }, rates);
        Assert.Equal(9.75m, rates.Sum());
    }

    [Fact]
    public void PublishedGraphiteRouteReferencePowersAndRankingArePreserved()
    {
        RecipeComparison comparison = RecipeComparison.Compare(new ProductionScenario(FuelKind.EnergeticGraphite, 60m));

        Assert.Equal(0, comparison.InfeasibleCount);
        Assert.Equal(new[] { 4.392m, -1.2m, -2.436m },
            comparison.Routes.Select(route => route.Balance.Energy.NetPowerMw).ToArray());
    }

    private static ProductionActivity Activity(string id, string primaryOutput,
        Dictionary<string, decimal> inputs, Dictionary<string, decimal> outputs)
    {
        var activity = new ProductionActivity(id, "machine", RecipeKind.Manufacturing, inputs, outputs,
            0m, 60m, 1m, null, 1m, 0m);
        activity.PrimaryOutputs.Add(primaryOutput);
        return activity;
    }
}
