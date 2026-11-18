namespace DspNetEnergy.Core;

internal static class ProductionChainCalculator
{
    internal static ProductionChainBalance Calculate(ProductionScenario scenario, Action<string, string>? onRecipeChosen = null)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        ArgumentOutOfRangeException.ThrowIfNegative(scenario.FuelPerMinute);
        if (scenario.OtherInfrastructureMw is < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(scenario.OtherInfrastructureMw));
        }
        LogisticsBalance? logistics = scenario.Logistics?.Calculate();

        string fuelId = GameCatalog.GetFuelId(scenario.Fuel);
        ProliferationOptions? options = scenario.Proliferation;
        (ProliferatorRule? rule, int spraysPerUnit) = GetProliferation(options);
        bool spraysFuel = options?.SprayFuel == true;
        EnergyBalance energy = EnergyCalculator.Burn(scenario.Fuel, scenario.FuelPerMinute,
            scenario.GeneratorCount, null, ExcludedCosts.None, spraysFuel ? rule : null);
        decimal fuelSprays = spraysFuel ? energy.BurnedFuelPerMinute : 0m;
        decimal fuelSprayUnits = spraysFuel ? fuelSprays / spraysPerUnit : 0m;

        ProductionActivity[] activities = new RecipeResolver(scenario, rule, onRecipeChosen).Resolve(fuelId, spraysFuel);
        var planner = new MaterialPlanner(activities, fuelId, rule?.ItemId);
        decimal[] rates = planner.Plan(scenario.FuelPerMinute, fuelSprayUnits, spraysPerUnit);
        decimal productionSprays = activities.Select((activity, index) => activity.SpraysPerCycle * rates[index]).Sum();
        SprayBalance? spray = options is null ? null : CalculateSpray(options, spraysPerUnit, productionSprays, fuelSprays);

        // Remove only the demand for spray. Keep the planned rates as ceilings so shared steps
        // and coproducts can contribute to the fuel without allocating their power twice.
        decimal[] fuelRates = spray?.ProliferatorUnitsPerMinute > 0m
            ? planner.Plan(scenario.FuelPerMinute, 0m, 0, rates)
            : rates;

        var report = new ChainReport(activities, rates, fuelRates, rule?.ItemId, spraysPerUnit);
        IReadOnlyDictionary<string, MaterialBalance> materials = report.Materials(fuelId, energy.BurnedFuelPerMinute,
            rule?.ItemId, fuelSprayUnits);
        var consumption = new ConsumptionBreakdown(report.ExtractionMw, report.FuelProductionMw,
            report.ProliferatorProductionMw,
            (spray?.CoaterWorkingPowerMw ?? 0m) + (logistics?.WorkingMw ?? 0m) + (scenario.OtherInfrastructureMw ?? 0m),
            report.IdleMw + (spray?.CoaterIdlePowerMw ?? 0m) + (logistics?.IdleMw ?? 0m));

        bool excludesLogistics = logistics?.HasExclusions ?? !scenario.OtherInfrastructureMw.HasValue;
        ExcludedCosts exclusions = excludesLogistics ? ExcludedCosts.SortersAndLogistics : ExcludedCosts.None;
        if (materials.Values.Any(material => material.SuppliedPerMinute > 0m))
        {
            exclusions |= ExcludedCosts.SuppliedMaterials;
        }

        decimal externalDysonPower = materials.Sum(entry => (entry.Value.ProducedPerMinute + entry.Value.SuppliedPerMinute)
            * GameCatalog.Items[entry.Key].ExternalEnergyMj / EnergyCalculator.SecondsPerMinute);
        return new ProductionChainBalance(energy with { ChainConsumption = consumption, ExcludedCosts = exclusions },
            consumption, report.Steps, materials, spray, externalDysonPower) { Logistics = logistics };
    }

    private static (ProliferatorRule? Rule, int SpraysPerUnit) GetProliferation(ProliferationOptions? options)
    {
        if (options is null)
        {
            return (null, 0);
        }

        if (!GameCatalog.Proliferators.TryGetValue(options.Tier, out ProliferatorRule? rule) ||
            options.CoaterCount is < 0 || options.ItemsPerMinutePerCoater <= 0m)
        {
            throw new ArgumentException("Invalid proliferator or spray coater configuration.", nameof(options));
        }

        if (options.SelfSpray && rule.SelfSprayedCapacity is null)
        {
            throw new ArgumentException("The selected proliferator does not support self-spraying.", nameof(options));
        }

        int spraysPerUnit = options.SelfSpray ? rule.SelfSprayedCapacity!.Value - 1 : rule.SpraysPerUnit;
        return (rule, spraysPerUnit);
    }

    private static SprayBalance CalculateSpray(ProliferationOptions options, int spraysPerUnit,
        decimal productionSprays, decimal fuelSprays)
    {
        decimal units = (productionSprays + fuelSprays) / spraysPerUnit;
        decimal selfSprays = options.SelfSpray ? units : 0m;
        decimal totalSprays = productionSprays + fuelSprays + selfSprays;
        decimal activeCoaters = totalSprays / options.ItemsPerMinutePerCoater;
        int count = EnergyCalculator.ResolveCount(options.CoaterCount, totalSprays, options.ItemsPerMinutePerCoater);
        if (activeCoaters - count > LinearPlan.Tolerance)
        {
            throw new InvalidOperationException("The installed spray coaters cannot process the requested throughput.");
        }

        Machine coater = GameCatalog.Machines["spray-coater"];
        return new SprayBalance(spraysPerUnit, productionSprays, fuelSprays, selfSprays, units, count,
            count == 0 ? 0m : activeCoaters / count, activeCoaters * coater.WorkingPowerMw,
            Math.Max(0m, count - activeCoaters) * coater.IdlePowerMw);
    }
}
