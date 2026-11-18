using DspNetEnergy.Core;
using Xunit;

namespace DspNetEnergy.Tests;

public sealed class EnergyCalculatorTests
{
    [Fact]
    public void CoalAndGraphiteReferenceAt120CoalPerMinute()
    {
        EnergyBalance coal = EnergyCalculator.Calculate(new DirectCombustionScenario(FuelKind.Coal, 120m));
        EnergyBalance graphite = EnergyCalculator.Calculate(new CoalToGraphiteScenario(120m));

        Assert.Equal(2, coal.GeneratorCount);
        Assert.Equal(5.40m, coal.FuelPowerMw);
        Assert.Equal(4.32m, coal.GrossPowerMw);
        Assert.Equal(120m, coal.BurnedFuelPerMinute);
        Assert.Equal(0m, coal.TotalConsumptionMw);
        Assert.Null(coal.Smelting);

        SmeltingBalance smelting = Assert.IsType<SmeltingBalance>(graphite.Smelting);
        Assert.Equal(2, smelting.SmelterCount);
        Assert.Equal(1m, smelting.Utilization);
        Assert.Equal(120m, smelting.ConsumedCoalPerMinute);
        Assert.Equal(0m, smelting.UnusedCoalPerMinute);
        Assert.Equal(60m, smelting.GraphitePerMinute);
        Assert.Equal(0.72m, smelting.WorkingPowerMw);
        Assert.Equal(0m, smelting.IdlePowerMw);
        Assert.Equal(3, graphite.GeneratorCount);
        Assert.Equal(6.48m, graphite.GeneratorCapacityMw);
        Assert.Equal(5.40m, graphite.GrossPowerMw);
        Assert.Equal(4.68m, graphite.NetPowerMw);
        Assert.Equal(0.36m, graphite.NetPowerMw - coal.NetPowerMw);
        var exclusions = ExcludedCosts.ResourceExtraction | ExcludedCosts.SortersAndLogistics;
        Assert.Equal(exclusions, coal.ExcludedCosts);
        Assert.Equal(exclusions, graphite.ExcludedCosts);
    }

    [Fact]
    public void FusionPowerIsCappedByInstalledGenerators()
    {
        EnergyBalance capped = EnergyCalculator.Calculate(new DirectCombustionScenario(FuelKind.DeuteronFuelRod, 60m, 10));
        EnergyBalance sized = EnergyCalculator.Calculate(new DirectCombustionScenario(FuelKind.DeuteronFuelRod, 60m));

        Assert.Equal(600m, capped.PotentialElectricPowerMw);
        Assert.Equal(150m, capped.GrossPowerMw);
        Assert.Equal(15m, capped.BurnedFuelPerMinute);
        Assert.Equal(45m, capped.UnburnedFuelPerMinute);
        Assert.Equal(40, sized.GeneratorCount);
        Assert.Equal(600m, sized.GrossPowerMw);
        Assert.Equal(0m, sized.UnburnedFuelPerMinute);
        Assert.True(sized.ExcludedCosts.HasFlag(ExcludedCosts.FuelProduction));
    }

    [Fact]
    public void SuppliedGraphiteCanLimitGenerationBelowCapacity()
    {
        EnergyBalance result = EnergyCalculator.Calculate(new DirectCombustionScenario(FuelKind.EnergeticGraphite, 12m, 2));

        Assert.Equal(4.32m, result.GeneratorCapacityMw);
        Assert.Equal(1.08m, result.GrossPowerMw);
        Assert.Equal(12m, result.BurnedFuelPerMinute);
        Assert.True(result.ExcludedCosts.HasFlag(ExcludedCosts.FuelProduction));
    }

    [Fact]
    public void SmelterCapacityLimitsProductionAndLeavesCoalUnused()
    {
        EnergyBalance result = EnergyCalculator.Calculate(new CoalToGraphiteScenario(120m, SmelterCount: 1));
        SmeltingBalance smelting = Assert.IsType<SmeltingBalance>(result.Smelting);

        Assert.Equal(60m, smelting.ConsumedCoalPerMinute);
        Assert.Equal(60m, smelting.UnusedCoalPerMinute);
        Assert.Equal(30m, smelting.GraphitePerMinute);
        Assert.Equal(2.70m, result.GrossPowerMw);
        Assert.Equal(2.34m, result.NetPowerMw);
    }

    [Fact]
    public void PartiallyUtilizedSmeltersConsumeWorkingAndIdlePower()
    {
        EnergyBalance result = EnergyCalculator.Calculate(new CoalToGraphiteScenario(360m, SmelterCount: 12));
        EnergyBalance fullyUtilized = EnergyCalculator.Calculate(new CoalToGraphiteScenario(360m, SmelterCount: 6));
        SmeltingBalance smelting = Assert.IsType<SmeltingBalance>(result.Smelting);

        Assert.Equal(0.5m, smelting.Utilization);
        Assert.Equal(180m, smelting.GraphitePerMinute);
        Assert.Equal(2.16m, smelting.WorkingPowerMw);
        Assert.Equal(0.072m, smelting.IdlePowerMw);
        Assert.Equal(0.072m, fullyUtilized.NetPowerMw - result.NetPowerMw);
    }

    [Fact]
    public void AutoSizingRoundsUpAndChargesIdlePowerForUnusedTime()
    {
        EnergyBalance result = EnergyCalculator.Calculate(new CoalToGraphiteScenario(90m));
        SmeltingBalance smelting = Assert.IsType<SmeltingBalance>(result.Smelting);

        Assert.Equal(2, smelting.SmelterCount);
        Assert.Equal(0.75m, smelting.Utilization);
        Assert.Equal(0.54m, smelting.WorkingPowerMw);
        Assert.Equal(0.006m, smelting.IdlePowerMw);
    }

    [Fact]
    public void GeneratorLimitReportsGraphiteSurplusAndPreservesSmeltingCost()
    {
        EnergyBalance result = EnergyCalculator.Calculate(new CoalToGraphiteScenario(120m, GeneratorCount: 2));

        Assert.Equal(4.32m, result.GrossPowerMw);
        Assert.Equal(48m, result.BurnedFuelPerMinute);
        Assert.Equal(12m, result.UnburnedFuelPerMinute);
        Assert.Equal(0.72m, result.TotalConsumptionMw);
        Assert.Equal(3.60m, result.NetPowerMw);
    }

    [Fact]
    public void IdleSmeltersStillConsumePowerWithoutCoal()
    {
        EnergyBalance result = EnergyCalculator.Calculate(new CoalToGraphiteScenario(0m, SmelterCount: 2));

        Assert.Equal(0, result.GeneratorCount);
        Assert.Equal(0m, result.GrossPowerMw);
        Assert.Equal(0m, Assert.IsType<SmeltingBalance>(result.Smelting).Utilization);
        Assert.Equal(-0.024m, result.NetPowerMw);
    }

    [Fact]
    public void ExplicitZeroSmeltersProcessNoCoal()
    {
        EnergyBalance result = EnergyCalculator.Calculate(new CoalToGraphiteScenario(120m, SmelterCount: 0));
        SmeltingBalance smelting = Assert.IsType<SmeltingBalance>(result.Smelting);

        Assert.Equal(120m, smelting.UnusedCoalPerMinute);
        Assert.Equal(0m, smelting.Utilization);
        Assert.Equal(0m, result.TotalConsumptionMw);
        Assert.Equal(0m, result.GrossPowerMw);
    }

    [Fact]
    public void InvalidScenarioValuesAreRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => EnergyCalculator.Calculate(new CoalToGraphiteScenario(-1m)));
        Assert.Throws<ArgumentOutOfRangeException>(() => EnergyCalculator.Calculate(new CoalToGraphiteScenario(1m, -1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => EnergyCalculator.Calculate(new DirectCombustionScenario(FuelKind.Coal, -1m)));
        Assert.Throws<ArgumentOutOfRangeException>(() => EnergyCalculator.Calculate(new DirectCombustionScenario(FuelKind.Coal, 1m, -1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => EnergyCalculator.Calculate(new DirectCombustionScenario((FuelKind)99, 1m)));
    }
}
