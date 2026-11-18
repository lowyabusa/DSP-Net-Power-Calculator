namespace DspNetEnergy.Core;

internal sealed class MaterialPlanner(ProductionActivity[] activities, string fuelId, string? sprayId)
{
    internal decimal[] Plan(decimal fuelPerMinute, decimal fuelSprayUnits, int spraysPerUnit,
        decimal[]? realizedRates = null)
    {
        string[] materials = activities.SelectMany(activity => activity.Inputs.Keys.Concat(activity.Outputs.Keys))
            .Append(fuelId).Distinct().Order(StringComparer.Ordinal).ToArray();
        var constraints = new List<PlanConstraint>();
        foreach (string material in materials)
        {
            var coefficients = new decimal[activities.Length];
            for (int i = 0; i < activities.Length; i++)
            {
                coefficients[i] = NetInput(activities[i], material, spraysPerUnit);
            }

            constraints.Add(new PlanConstraint(coefficients, -Demand(material, fuelPerMinute, fuelSprayUnits)));
        }

        for (int i = 0; i < activities.Length; i++)
        {
            decimal? capacity = realizedRates?[i] ?? activities[i].Capacity;
            if (capacity is decimal limit)
            {
                var coefficients = new decimal[activities.Length];
                coefficients[i] = 1m;
                constraints.Add(new PlanConstraint(coefficients, limit));
            }

            if (realizedRates is null && activities[i].PrimaryOutputs.Count > 0)
            {
                constraints.Add(PrimaryDemandLimit(i, fuelPerMinute, fuelSprayUnits, spraysPerUnit));
            }
        }

        decimal[]? rates = realizedRates is null
            ? SolveWithCoproductCredits(constraints, materials)
            : new LinearPlan(constraints, activities.Length).Solve();
        return rates ?? throw new InvalidOperationException(
            "The selected recipes, supplies and machine capacities cannot meet the requested fuel throughput.");
    }

    private decimal[]? SolveWithCoproductCredits(List<PlanConstraint> constraints, string[] materials)
    {
        decimal[]? rates = new LinearPlan(constraints, activities.Length).Solve();
        if (rates is null)
        {
            return null;
        }

        for (int index = 0; index < activities.Length; index++)
        {
            ProductionActivity activity = activities[index];
            if (rates[index] <= LinearPlan.Tolerance || activity.PrimaryOutputs.Count == 0)
            {
                continue;
            }

            PlanConstraint[] balances = activity.PrimaryOutputs.Order(StringComparer.Ordinal)
                .Select(item => constraints[Array.IndexOf(materials, item)]).ToArray();
            if (balances.Any(balance => Surplus(balance, rates) <= 0.000000000001m))
            {
                continue;
            }

            // A recipe can be skipped or have one selected output fully used.
            // Choose the feasible branch with the smallest total activity.
            var skip = new decimal[activities.Length];
            skip[index] = 1m;
            decimal[]? best = SolveWith(new PlanConstraint(skip, 0m));
            foreach (PlanConstraint balance in balances)
            {
                var tight = new PlanConstraint(balance.Coefficients.Select(value => -value).ToArray(), -balance.Limit);
                decimal[]? candidate = SolveWith(tight);
                if (candidate is not null && (best is null || candidate.Sum() < best.Sum()))
                {
                    best = candidate;
                }
            }

            return best;
        }

        return rates;

        decimal[]? SolveWith(PlanConstraint constraint)
        {
            constraints.Add(constraint);
            decimal[]? result = SolveWithCoproductCredits(constraints, materials);
            constraints.RemoveAt(constraints.Count - 1);
            return result;
        }
    }

    private static decimal Surplus(PlanConstraint balance, decimal[] rates) =>
        balance.Limit - balance.Coefficients.Select((value, index) => value * rates[index]).Sum();

    private PlanConstraint PrimaryDemandLimit(int index, decimal fuelPerMinute, decimal fuelSprayUnits, int spraysPerUnit)
    {
        // Coproducts may satisfy other material balances. They alone may not drive additional runs
        // of a recipe selected only for its primary outputs.
        var coefficients = new decimal[activities.Length];
        coefficients[index] = 1m;
        decimal limit = 0m;
        foreach (string material in activities[index].PrimaryOutputs)
        {
            decimal yield = -NetInput(activities[index], material, spraysPerUnit);
            if (yield <= 0m)
            {
                throw new InvalidOperationException($"Recipe '{activities[index].Id}' cannot supply '{material}' after its own consumption.");
            }

            limit += Demand(material, fuelPerMinute, fuelSprayUnits) / yield;
            for (int consumer = 0; consumer < activities.Length; consumer++)
            {
                coefficients[consumer] -= Math.Max(0m, NetInput(activities[consumer], material, spraysPerUnit)) / yield;
            }
        }

        return new PlanConstraint(coefficients, limit);
    }

    private decimal Demand(string material, decimal fuelPerMinute, decimal fuelSprayUnits) =>
        (material == fuelId ? fuelPerMinute : 0m) + (material == sprayId ? fuelSprayUnits : 0m);

    private decimal NetInput(ProductionActivity activity, string material, int spraysPerUnit)
    {
        decimal sprayInput = material == sprayId && spraysPerUnit > 0 ? activity.SpraysPerCycle / spraysPerUnit : 0m;
        return activity.Inputs.GetValueOrDefault(material) + sprayInput - activity.Outputs.GetValueOrDefault(material);
    }
}
