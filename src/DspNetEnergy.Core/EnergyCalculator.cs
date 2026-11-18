namespace DspNetEnergy.Core;

public static class EnergyCalculator
{
    internal const decimal SecondsPerMinute = 60m;
    private const ExcludedCosts CommonExclusions = ExcludedCosts.ResourceExtraction | ExcludedCosts.SortersAndLogistics;

    public static ProductionChainBalance Calculate(ProductionScenario scenario) => ProductionChainCalculator.Calculate(scenario);

    public static EnergyBalance Calculate(DirectCombustionScenario scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        ArgumentOutOfRangeException.ThrowIfNegative(scenario.AvailableFuelPerMinute);

        ExcludedCosts exclusions = CommonExclusions;
        if (scenario.Fuel != FuelKind.Coal)
        {
            exclusions |= ExcludedCosts.FuelProduction;
        }

        return Burn(scenario.Fuel, scenario.AvailableFuelPerMinute, scenario.GeneratorCount, null, exclusions);
    }

    public static EnergyBalance Calculate(CoalToGraphiteScenario scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        ArgumentOutOfRangeException.ThrowIfNegative(scenario.AvailableCoalPerMinute);
        var recipe = GameCatalog.Recipes["energetic-graphite"];
        var machine = GameCatalog.Machines["arc-smelter"];
        decimal coalPerCycle = recipe.Inputs["coal"];

        decimal coalPerSmelterPerMinute = SecondsPerMinute * machine.Speed * coalPerCycle / recipe.CycleSeconds;
        int smelterCount = ResolveCount(scenario.SmelterCount, scenario.AvailableCoalPerMinute, coalPerSmelterPerMinute);
        decimal consumedCoal = Math.Min(scenario.AvailableCoalPerMinute, smelterCount * coalPerSmelterPerMinute);
        decimal activeSmelters = consumedCoal / coalPerSmelterPerMinute;
        decimal graphite = consumedCoal * recipe.Outputs["energetic-graphite"] / coalPerCycle;

        var smelting = new SmeltingBalance(
            smelterCount,
            smelterCount == 0 ? 0m : activeSmelters / smelterCount,
            consumedCoal,
            scenario.AvailableCoalPerMinute - consumedCoal,
            graphite,
            activeSmelters * machine.WorkingPowerMw,
            (smelterCount - activeSmelters) * machine.IdlePowerMw);

        return Burn(FuelKind.EnergeticGraphite, graphite, scenario.GeneratorCount, smelting, CommonExclusions);
    }

    internal static EnergyBalance Burn(
        FuelKind kind,
        decimal availableFuelPerMinute,
        int? generatorCount,
        SmeltingBalance? smelting,
        ExcludedCosts exclusions,
        ProliferatorRule? spray = null)
    {
        Item fuel = GameCatalog.Items[GameCatalog.GetFuelId(kind)];
        Generator generator = GameCatalog.GetGenerator(fuel);
        decimal energyMultiplier = generator.SprayBoostsCapacityOnly ? 1m : spray?.ExtraProductsMultiplier ?? 1m;
        decimal capacityMultiplier = generator.SprayBoostsCapacityOnly ? spray?.SpeedMultiplier ?? 1m : energyMultiplier;
        decimal energyPerItem = fuel.EnergyMj * energyMultiplier;
        decimal capacityPerGenerator = generator.MaxPowerMw * fuel.GeneratorCapacityMultiplier * capacityMultiplier;
        decimal fuelPower = availableFuelPerMinute * energyPerItem / SecondsPerMinute;
        decimal potentialElectricPower = fuelPower * generator.Efficiency;
        int count = ResolveCount(generatorCount, potentialElectricPower, capacityPerGenerator);
        decimal generatorCapacity = count * capacityPerGenerator;
        decimal grossPower = Math.Min(potentialElectricPower, generatorCapacity);
        decimal burnedFuel = potentialElectricPower <= generatorCapacity
            ? availableFuelPerMinute
            : grossPower * SecondsPerMinute / (energyPerItem * generator.Efficiency);

        return new EnergyBalance(availableFuelPerMinute, burnedFuel, fuelPower, potentialElectricPower,
            count, generatorCapacity, grossPower, smelting, exclusions) { FuelEnergyMjPerItem = energyPerItem };
    }

    internal static int ResolveCount(int? count, decimal demand, decimal capacityPerBuilding)
    {
        if (count is int specifiedCount)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(specifiedCount);
            return specifiedCount;
        }

        return (int)decimal.Ceiling(demand / capacityPerBuilding);
    }
}
