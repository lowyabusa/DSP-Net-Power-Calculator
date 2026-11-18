using System.Collections.ObjectModel;

namespace DspNetEnergy.Core;

internal sealed class ChainReport
{
    private readonly Dictionary<string, MaterialBalance> materials = new();
    internal IReadOnlyList<ProductionStepBalance> Steps { get; }
    internal decimal ExtractionMw { get; }
    internal decimal FuelProductionMw { get; }
    internal decimal ProliferatorProductionMw { get; }
    internal decimal IdleMw { get; }

    internal ChainReport(ProductionActivity[] activities, decimal[] rates, decimal[] fuelRates,
        string? sprayId, int spraysPerUnit)
    {
        var steps = new List<ProductionStepBalance>();
        for (int i = 0; i < activities.Length; i++)
        {
            ProductionActivity activity = activities[i];
            decimal rate = rates[i];
            if (activity.MachineId is null)
            {
                if (rate > 0m)
                {
                    materials[activity.Id] = GetMaterial(activity.Id) with { SuppliedPerMinute = rate };
                }

                continue;
            }

            if (rate == 0m && activity.InstalledMachines is not > 0)
            {
                continue;
            }

            Dictionary<string, decimal> inputs = Scale(activity.Inputs, rate);
            Dictionary<string, decimal> outputs = Scale(activity.Outputs, rate);
            if (sprayId is not null && activity.SpraysPerCycle > 0m)
            {
                inputs[sprayId] = inputs.GetValueOrDefault(sprayId) + activity.SpraysPerCycle * rate / spraysPerUnit;
            }

            foreach (var (item, amount) in inputs)
            {
                MaterialBalance material = GetMaterial(item);
                materials[item] = material with { UsedInProductionPerMinute = material.UsedInProductionPerMinute + amount };
            }

            foreach (var (item, amount) in outputs)
            {
                MaterialBalance material = GetMaterial(item);
                materials[item] = material with { ProducedPerMinute = material.ProducedPerMinute + amount };
            }

            decimal active = rate / activity.CyclesPerMinutePerMachine;
            decimal usableCapacity = activity.MaximumUtilization * activity.CyclesPerMinutePerMachine;
            int count = EnergyCalculator.ResolveCount(activity.InstalledMachines, rate, usableCapacity);
            decimal work = active * activity.WorkingPowerMwPerMachine;
            decimal idle = Math.Max(0m, count - active) * activity.IdlePowerMwPerMachine;
            decimal fuelWork = Math.Min(rate, fuelRates[i]) / activity.CyclesPerMinutePerMachine * activity.WorkingPowerMwPerMachine;
            if (activity.Kind == RecipeKind.Extraction)
            {
                ExtractionMw += fuelWork;
            }
            else
            {
                FuelProductionMw += fuelWork;
            }

            ProliferatorProductionMw += work - fuelWork;
            IdleMw += idle;
            steps.Add(new ProductionStepBalance(activity.Id, activity.MachineId, count, count == 0 ? 0m : active / count,
                rate, work, idle, new ReadOnlyDictionary<string, decimal>(inputs), new ReadOnlyDictionary<string, decimal>(outputs)));
        }

        Steps = steps.AsReadOnly();
    }

    internal IReadOnlyDictionary<string, MaterialBalance> Materials(string fuelId, decimal burnedFuel,
        string? sprayId, decimal combustionSprayUnits)
    {
        materials[fuelId] = GetMaterial(fuelId) with { BurnedPerMinute = burnedFuel };
        if (sprayId is not null && combustionSprayUnits > 0m)
        {
            MaterialBalance spray = GetMaterial(sprayId);
            materials[sprayId] = spray with { UsedInProductionPerMinute = spray.UsedInProductionPerMinute + combustionSprayUnits };
        }

        return new ReadOnlyDictionary<string, MaterialBalance>(materials);
    }

    private MaterialBalance GetMaterial(string id) => materials.GetValueOrDefault(id) ?? new MaterialBalance(0m, 0m, 0m, 0m);

    private static Dictionary<string, decimal> Scale(IReadOnlyDictionary<string, decimal> amounts, decimal rate) =>
        amounts.ToDictionary(entry => entry.Key, entry => entry.Value * rate);
}
