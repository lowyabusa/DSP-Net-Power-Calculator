namespace DspNetEnergy.Core;

internal sealed record ProductionActivity(
    string Id,
    string? MachineId,
    RecipeKind Kind,
    IReadOnlyDictionary<string, decimal> Inputs,
    IReadOnlyDictionary<string, decimal> Outputs,
    decimal SpraysPerCycle,
    decimal CyclesPerMinutePerMachine,
    decimal MaximumUtilization,
    int? InstalledMachines,
    decimal WorkingPowerMwPerMachine,
    decimal IdlePowerMwPerMachine)
{
    internal HashSet<string> PrimaryOutputs { get; } = new();

    internal decimal? Capacity => InstalledMachines * MaximumUtilization * CyclesPerMinutePerMachine;

    internal static ProductionActivity Supply(string itemId) => new(itemId, null, RecipeKind.Extraction,
        new Dictionary<string, decimal>(), new Dictionary<string, decimal> { [itemId] = 1m },
        0m, 1m, 1m, null, 0m, 0m);

    internal static ProductionActivity Create(string recipeId, Recipe recipe, ProductionStepSettings settings,
        ProliferatorRule? rule)
    {
        string machineId = settings.MachineId ?? recipe.Machines[0];
        if (!recipe.Machines.Contains(machineId))
        {
            throw new ArgumentException($"Machine '{machineId}' cannot run recipe '{recipeId}'.");
        }

        if (settings.MachineCount is < 0 || settings.MaximumUtilization is <= 0m or > 1m ||
            settings.RateMultiplier <= 0m || settings.WorkingPowerMw is < 0m || settings.IdlePowerMw is < 0m)
        {
            throw new ArgumentException($"Invalid machine settings for recipe '{recipeId}'.");
        }

        if (!recipe.AllowsSpray(settings.Spray) || (settings.Spray != SprayMode.None && rule is null))
        {
            throw new ArgumentException($"Spray mode '{settings.Spray}' is unavailable for recipe '{recipeId}'.");
        }

        Machine machine = GameCatalog.Machines[machineId];
        decimal cycleRate = EnergyCalculator.SecondsPerMinute * machine.Speed * settings.RateMultiplier / recipe.CycleSeconds;
        decimal workingPower = settings.WorkingPowerMw ?? machine.WorkingPowerMw;
        decimal idlePower = settings.IdlePowerMw ?? machine.IdlePowerMw;
        Dictionary<string, decimal> outputs = new(recipe.Outputs);
        decimal sprays = 0m;
        if (settings.Spray != SprayMode.None && rule is not null)
        {
            workingPower *= rule.WorkingPowerMultiplier;
            sprays = recipe.Inputs.Values.Sum();
            switch (settings.Spray, recipe.Kind)
            {
                case (SprayMode.ExtraProducts, _):
                    ScaleOutputs(outputs, rule.ExtraProductsMultiplier);
                    break;
                case (SprayMode.Speed, RecipeKind.Fractionation):
                    outputs["deuterium"] *= rule.SpeedMultiplier;
                    outputs["hydrogen"] = recipe.Inputs["hydrogen"] - outputs["deuterium"];
                    sprays = outputs["deuterium"];
                    break;
                case (SprayMode.Speed, RecipeKind.PhotonCapture):
                    ScaleOutputs(outputs, rule.SpeedMultiplier);
                    break;
                case (SprayMode.Speed, _):
                    cycleRate *= rule.SpeedMultiplier;
                    break;
            }
        }

        return new ProductionActivity(recipeId, machineId, recipe.Kind, recipe.Inputs, outputs, sprays,
            cycleRate, settings.MaximumUtilization, settings.MachineCount, workingPower, idlePower);
    }

    private static void ScaleOutputs(Dictionary<string, decimal> outputs, decimal multiplier)
    {
        foreach (string item in outputs.Keys)
        {
            outputs[item] *= multiplier;
        }
    }
}
