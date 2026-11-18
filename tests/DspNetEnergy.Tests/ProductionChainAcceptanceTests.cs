using DspNetEnergy.Core;
using Xunit;

namespace DspNetEnergy.Tests;

public sealed class ProductionChainAcceptanceTests
{
    [Fact]
    public void GraphiteChainIncludesMiningAndPreservesTheReferenceBoundary()
    {
        var scenario = new ProductionScenario(FuelKind.EnergeticGraphite, 60m);
        ProductionChainBalance full = EnergyCalculator.Calculate(scenario);
        ProductionChainBalance supplied = EnergyCalculator.Calculate(scenario with { SuppliedItems = Items("coal") });

        Close(120m, full.Materials["coal"].ProducedPerMinute);
        Close(0.28m, full.Consumption.ResourceExtractionMw);
        Close(0.72m, full.Consumption.FuelProductionMw);
        Close(0.008m, full.Consumption.IdleMw);
        Close(4.392m, full.Energy.NetPowerMw);
        Close(4.68m, supplied.Energy.NetPowerMw);
        Assert.True(supplied.Energy.ExcludedCosts.HasFlag(ExcludedCosts.SuppliedMaterials));
        Assert.True(supplied.Energy.ExcludedCosts.HasFlag(ExcludedCosts.SortersAndLogistics));
        AssertBalanced(full);
        AssertBalanced(supplied);
    }

    [Fact]
    public void AlternativeGraphiteRecipeNetsRecycledHydrogenAndUsesOneOilProducer()
    {
        var scenario = new ProductionScenario(FuelKind.EnergeticGraphite, 30m)
        {
            RecipeChoices = new Dictionary<string, string> { ["energetic-graphite"] = "x-ray-cracking" },
        };
        ProductionChainBalance result = EnergyCalculator.Calculate(scenario);

        Close(30m, Step(result, "x-ray-cracking").CyclesPerMinute);
        Close(15m, Step(result, "plasma-refining").CyclesPerMinute);
        Close(30m, result.Materials["crude-oil"].ProducedPerMinute);
        Close(105m, result.Materials["hydrogen"].ProducedPerMinute);
        Close(60m, result.Materials["hydrogen"].UsedInProductionPerMinute);
        Close(45m, result.Materials["hydrogen"].SurplusPerMinute);
        Assert.DoesNotContain(result.Steps, step => step.RecipeId is "energetic-graphite" or "coal-vein");
        AssertBalanced(result);
    }

    [Theory]
    [InlineData(10, 20, 16800, 0)]
    [InlineData(24, 9, 7000, 16)]
    [InlineData(25, 8, 6720, 0)]
    public void OilSeepFlowChangesExtractorCountAndPowerAtTheSameRefineryThroughput(
        int tenthsOfCrudeOilPerSecond, int extractorCount, int workingKilowatts, int idleKilowatts)
    {
        var scenario = new ProductionScenario(FuelKind.HydrogenFuelRod, 120m)
        {
            Proliferation = new(ProliferatorTier.MkIII, SprayFuel: true),
            RecipeChoices = new Dictionary<string, string> { ["hydrogen"] = "plasma-refining" },
            Steps = new Dictionary<string, ProductionStepSettings>
            {
                ["crude-oil-seep"] = new(RateMultiplier: tenthsOfCrudeOilPerSecond / 10m),
            },
        };
        ProductionChainBalance result = EnergyCalculator.Calculate(scenario);

        ProductionStepBalance refining = Step(result, "plasma-refining");
        Assert.Equal(40, refining.MachineCount);
        Close(1200m, refining.InputsPerMinute["crude-oil"]);
        Close(600m, refining.OutputsPerMinute["hydrogen"]);

        ProductionStepBalance extraction = Step(result, "crude-oil-seep");
        Assert.Equal(extractorCount, extraction.MachineCount);
        Close(1200m, extraction.OutputsPerMinute["crude-oil"]);
        Close(workingKilowatts / 1000m, extraction.WorkingPowerMw);
        Close(idleKilowatts / 1000m, extraction.IdlePowerMw);
        AssertBalanced(result);
    }

    [Fact]
    public void ComplementaryRefineryRecipesCloseTheCatalystLoop()
    {
        ProductionChainBalance result = EnergyCalculator.Calculate(new ProductionScenario(FuelKind.EnergeticGraphite, 30m)
        {
            RecipeChoices = new Dictionary<string, string>
            {
                ["energetic-graphite"] = "x-ray-cracking",
                ["refined-oil"] = "reforming-refine",
            },
        });

        Close(30m, Step(result, "x-ray-cracking").CyclesPerMinute);
        Close(30m, Step(result, "reforming-refine").CyclesPerMinute);
        Close(30m, result.Materials["coal"].UsedInProductionPerMinute);
        Close(0m, result.Materials["hydrogen"].SurplusPerMinute);
        Close(0m, result.Materials["refined-oil"].SurplusPerMinute);
        Assert.DoesNotContain(result.Steps, step => step.RecipeId == "plasma-refining");
        AssertBalanced(result);
    }

    [Theory]
    [InlineData(SprayMode.ExtraProducts, 96, 1440, 2)]
    [InlineData(SprayMode.Speed, 120, 900, 1)]
    public void MkIIIProductionModesMatchTheSpecifiedEnergyPerGraphite(
        SprayMode mode, int coal, int workingKilowatts, int smelters)
    {
        ProductionChainBalance result = EnergyCalculator.Calculate(new ProductionScenario(FuelKind.EnergeticGraphite, 60m)
        {
            Steps = new Dictionary<string, ProductionStepSettings> { ["energetic-graphite"] = new(Spray: mode) },
            SuppliedItems = Items("proliferator-3"),
            Proliferation = new(ProliferatorTier.MkIII),
        });

        ProductionStepBalance smelting = Step(result, "energetic-graphite");
        Close(coal, smelting.InputsPerMinute["coal"]);
        Close(workingKilowatts / 1000m, smelting.WorkingPowerMw);
        Assert.Equal(smelters, smelting.MachineCount);
        SprayBalance spray = Assert.IsType<SprayBalance>(result.Spray);
        Close(coal, spray.ProductionSpraysPerMinute);
        Close(coal / 60m, spray.ProliferatorUnitsPerMinute);
        AssertBalanced(result);
    }

    [Theory]
    [InlineData(ProliferatorTier.MkI, 12, 1125)]
    [InlineData(ProliferatorTier.MkII, 24, 1200)]
    [InlineData(ProliferatorTier.MkIII, 60, 1250)]
    public void NormalFuelSprayIncreasesBothEnergyAndGeneratorCapacity(ProliferatorTier tier, int charges, int multiplier)
    {
        ProductionChainBalance result = EnergyCalculator.Calculate(new ProductionScenario(FuelKind.Coal, 60m, 1)
        {
            Proliferation = new(tier, SprayFuel: true),
        });

        Close(2.70m * multiplier / 1000m, result.Energy.FuelEnergyMjPerItem);
        Close(2.16m * multiplier / 1000m, result.Energy.GeneratorCapacityMw);
        Close(60m, result.Energy.BurnedFuelPerMinute);
        Assert.Equal(charges, Assert.IsType<SprayBalance>(result.Spray).UsableSpraysPerUnit);
        AssertBalanced(result);
    }

    [Theory]
    [InlineData(ProliferatorTier.MkI, "proliferator-1", 12, 12)]
    [InlineData(ProliferatorTier.MkII, "proliferator-2", 24, 27)]
    public void LowerTierSelfSprayUsesOneChargeAndAddsCoaterLoad(
        ProliferatorTier tier, string sprayItem, int plainCharges, int usableCharges)
    {
        var scenario = new ProductionScenario(FuelKind.Coal, usableCharges, 1)
        {
            Proliferation = new(tier, SprayFuel: true, SelfSpray: true,
                CoaterCount: 1, ItemsPerMinutePerCoater: 60m),
        };
        ProductionChainBalance result = EnergyCalculator.Calculate(scenario);
        ProductionChainBalance plain = EnergyCalculator.Calculate(
            scenario with { Proliferation = scenario.Proliferation! with { SelfSpray = false } });

        SprayBalance spray = Assert.IsType<SprayBalance>(result.Spray);
        Assert.Equal(usableCharges, spray.UsableSpraysPerUnit);
        Close(usableCharges, spray.FuelSpraysPerMinute);
        Close(1m, spray.SelfSpraysPerMinute);
        Close(1m, spray.ProliferatorUnitsPerMinute);
        Close(1m, result.Materials[sprayItem].ProducedPerMinute);
        Close(1m, result.Materials[sprayItem].UsedInProductionPerMinute);
        Close((usableCharges + 1m) / 60m, spray.CoaterUtilization);
        Close((usableCharges + 1m) * 0.09m / 60m, spray.CoaterWorkingPowerMw);
        Close((59m - usableCharges) * 0.0045m / 60m, spray.CoaterIdlePowerMw);
        Close((decimal)usableCharges / plainCharges, Assert.IsType<SprayBalance>(plain.Spray).ProliferatorUnitsPerMinute);
        Close(plain.Energy.GrossPowerMw, result.Energy.GrossPowerMw);
        AssertBalanced(result);
    }

    [Fact]
    public void SelfSprayedMkIIIProvides74ExternalSpraysAndAccountsForTheCoater()
    {
        ProductionChainBalance result = EnergyCalculator.Calculate(new ProductionScenario(FuelKind.Coal, 74m)
        {
            Proliferation = new(ProliferatorTier.MkIII, SprayFuel: true, SelfSpray: true,
                CoaterCount: 2, ItemsPerMinutePerCoater: 150m),
            OtherInfrastructureMw = 0.020m,
        });

        SprayBalance spray = Assert.IsType<SprayBalance>(result.Spray);
        Assert.Equal(74, spray.UsableSpraysPerUnit);
        Close(74m, spray.FuelSpraysPerMinute);
        Close(1m, spray.SelfSpraysPerMinute);
        Close(1m, spray.ProliferatorUnitsPerMinute);
        Close(1m, result.Materials["proliferator-3"].ProducedPerMinute);
        Close(1m, result.Materials["proliferator-3"].UsedInProductionPerMinute);
        Close(0m, result.Materials["proliferator-3"].SuppliedPerMinute);
        Close(0.25m, spray.CoaterUtilization);
        Close(0.045m, spray.CoaterWorkingPowerMw);
        Close(0.00675m, spray.CoaterIdlePowerMw);
        Close(0.065m, result.Consumption.SprayAndLogisticsMw);
        Assert.True(result.Consumption.ProliferatorProductionMw > 0m);
        Assert.Equal(ExcludedCosts.None, result.Energy.ExcludedCosts);
        AssertBalanced(result, 0.020m);
    }

    [Fact]
    public void JointSprayAndFuelPlanAccountsForSharedProductionOnlyOnce()
    {
        ProductionChainBalance result = EnergyCalculator.Calculate(new ProductionScenario(FuelKind.EnergeticGraphite, 60m)
        {
            Proliferation = new(ProliferatorTier.MkIII, SprayFuel: true, SelfSpray: true),
            Steps = new Dictionary<string, ProductionStepSettings>
            {
                ["energetic-graphite"] = new(Spray: SprayMode.ExtraProducts),
            },
        });

        SprayBalance spray = Assert.IsType<SprayBalance>(result.Spray);
        ProductionStepBalance smelting = Step(result, "energetic-graphite");
        Assert.True(smelting.OutputsPerMinute["energetic-graphite"] > 60m);
        Close(60m + result.Materials["energetic-graphite"].UsedInProductionPerMinute,
            smelting.OutputsPerMinute["energetic-graphite"]);
        Close(smelting.InputsPerMinute["coal"], spray.ProductionSpraysPerMinute);
        Close((60m + spray.ProductionSpraysPerMinute) / 74m, result.Materials["proliferator-3"].ProducedPerMinute);
        Close(1.44m, result.Consumption.FuelProductionMw);
        Assert.True(result.Consumption.ProliferatorProductionMw > 0m);
        AssertBalanced(result);
    }

    [Fact]
    public void AntimatterSprayChangesMaximumPowerWithoutChangingEnergyPerRod()
    {
        var scenario = new ProductionScenario(FuelKind.AntimatterFuelRod, 1.2m, 1)
        {
            SuppliedItems = Items("annihilation-constraint-sphere", "titanium-alloy"),
        };
        ProductionChainBalance plain = EnergyCalculator.Calculate(scenario);
        ProductionChainBalance sprayed = EnergyCalculator.Calculate(scenario with
        {
            Proliferation = new(ProliferatorTier.MkIII, SprayFuel: true),
        });

        Close(7200m, plain.Energy.FuelEnergyMjPerItem);
        Close(7200m, sprayed.Energy.FuelEnergyMjPerItem);
        Close(144m, plain.Energy.FuelPowerMw);
        Close(144m, sprayed.Energy.FuelPowerMw);
        Close(72m, plain.Energy.GrossPowerMw);
        Close(144m, sprayed.Energy.GrossPowerMw);
        Close(0.6m, plain.Energy.BurnedFuelPerMinute);
        Close(1.2m, sprayed.Energy.BurnedFuelPerMinute);
        Close(7.2m, plain.Materials["hydrogen"].ProducedPerMinute);
        Close(0m, plain.Materials["hydrogen"].SurplusPerMinute);
        Close(144m, plain.ExternalDysonPowerMw);
        Close(144m, sprayed.ExternalDysonPowerMw);
        Assert.DoesNotContain(plain.Steps, step => step.RecipeId == "plasma-refining");
        AssertBalanced(plain);
        AssertBalanced(sprayed);
    }

    [Fact]
    public void StrangeRodsHaveTheirOwnCapacityAndResolveTheFullChain()
    {
        var scenario = new ProductionScenario(FuelKind.StrangeAnnihilationFuelRod, 0.24m, 1);
        ProductionChainBalance plain = EnergyCalculator.Calculate(scenario);
        ProductionChainBalance sprayed = EnergyCalculator.Calculate(scenario with
        {
            Proliferation = new(ProliferatorTier.MkIII, SprayFuel: true),
        });

        Close(72000m, plain.Energy.FuelEnergyMjPerItem);
        Close(72000m, sprayed.Energy.FuelEnergyMjPerItem);
        Close(144m, plain.Energy.GeneratorCapacityMw);
        Close(288m, sprayed.Energy.GeneratorCapacityMw);
        Close(0.24m, sprayed.Materials["df-core-element"].SuppliedPerMinute);
        Close(230.4m, sprayed.ExternalDysonPowerMw);
        AssertBalanced(plain);
        AssertBalanced(sprayed);
    }

    [Fact]
    public void FractionationSpraysOnlyFreshHydrogenAndCreditsGasCoproduct()
    {
        ProductionChainBalance result = EnergyCalculator.Calculate(new ProductionScenario(FuelKind.DeuteronFuelRod, 3m)
        {
            RecipeChoices = new Dictionary<string, string>
            {
                ["deuterium"] = "deuterium-fractionation",
                ["hydrogen"] = "gas-giant",
            },
            Steps = new Dictionary<string, ProductionStepSettings>
            {
                ["deuterium-fractionation"] = new(Spray: SprayMode.Speed),
            },
            Proliferation = new(ProliferatorTier.MkIII),
            SuppliedItems = Items("super-magnetic-ring", "titanium-alloy", "proliferator-3"),
        });

        ProductionStepBalance fractionation = Step(result, "deuterium-fractionation");
        ProductionStepBalance gas = Step(result, "gas-giant");
        Close(28.8m, fractionation.OutputsPerMinute["deuterium"]);
        Close(1.2m, gas.OutputsPerMinute["deuterium"]);
        Close(28.8m, gas.OutputsPerMinute["hydrogen"]);
        Close(1440m, fractionation.InputsPerMinute["hydrogen"]);
        Close(1411.2m, fractionation.OutputsPerMinute["hydrogen"]);
        Close(28.8m, Assert.IsType<SprayBalance>(result.Spray).ProductionSpraysPerMinute);
        AssertBalanced(result);
    }

    [Fact]
    public void SelectedMachineTypeAndUtilizationControlInstalledAndIdlePower()
    {
        ProductionChainBalance result = EnergyCalculator.Calculate(new ProductionScenario(FuelKind.EnergeticGraphite, 60m)
        {
            Steps = new Dictionary<string, ProductionStepSettings>
            {
                ["energetic-graphite"] = new("plane-smelter", MaximumUtilization: 0.5m),
            },
        });

        ProductionStepBalance step = Step(result, "energetic-graphite");
        Assert.Equal(2, step.MachineCount);
        Close(0.5m, step.Utilization);
        Close(1.44m, step.WorkingPowerMw);
        Close(0.048m, step.IdlePowerMw);
        AssertBalanced(result);
    }

    [Fact]
    public void InsufficientMachinesOrCoatersReportAnInfeasibleScenario()
    {
        var scenario = new ProductionScenario(FuelKind.EnergeticGraphite, 60m);
        Assert.Throws<InvalidOperationException>(() => EnergyCalculator.Calculate(scenario with
        {
            Steps = new Dictionary<string, ProductionStepSettings> { ["energetic-graphite"] = new(MachineCount: 1) },
        }));
        Assert.Throws<InvalidOperationException>(() => EnergyCalculator.Calculate(scenario with
        {
            Proliferation = new(ProliferatorTier.MkI, SprayFuel: true, CoaterCount: 0),
        }));
    }

    [Fact]
    public void AntimatterManufacturingRejectsExtraProducts()
    {
        Assert.Throws<ArgumentException>(() => EnergyCalculator.Calculate(new ProductionScenario(FuelKind.AntimatterFuelRod, 1m)
        {
            Proliferation = new(ProliferatorTier.MkIII),
            Steps = new Dictionary<string, ProductionStepSettings>
            {
                ["antimatter-fuel-rod"] = new(Spray: SprayMode.ExtraProducts),
            },
        }));
    }

    private static HashSet<string> Items(params string[] items) => new(items);

    private static ProductionStepBalance Step(ProductionChainBalance balance, string id) =>
        Assert.Single(balance.Steps, step => step.RecipeId == id);

    private static void AssertBalanced(ProductionChainBalance result, decimal infrastructure = 0m)
    {
        Assert.All(result.Materials, entry => Assert.True(entry.Value.SurplusPerMinute >= -0.000000001m,
            $"Deficit for {entry.Key}: {entry.Value.SurplusPerMinute} per minute."));
        decimal total = result.Steps.Sum(step => step.WorkingPowerMw + step.IdlePowerMw) + infrastructure
            + (result.Spray?.CoaterWorkingPowerMw ?? 0m) + (result.Spray?.CoaterIdlePowerMw ?? 0m);
        Close(total, result.Consumption.TotalMw);
        Close(total, result.Energy.TotalConsumptionMw);
        Close(result.Energy.GrossPowerMw - total, result.Energy.NetPowerMw);
    }

    private static void Close(decimal expected, decimal actual) => Assert.True(Math.Abs(expected - actual) <= 0.000000001m,
        $"Expected {expected}, actual {actual}.");
}
