using System.Collections.ObjectModel;
using System.Globalization;

namespace DspNetEnergy.Core;

public sealed record RecipeRoute(IReadOnlyDictionary<string, string> RecipeChoices, ProductionChainBalance Balance,
    bool IsCurrent);

public sealed record RecipeComparison(IReadOnlyList<RecipeRoute> Routes, int InfeasibleCount)
{
    /// <summary>Ranks reachable recipe combinations at the same fuel target and factory settings.</summary>
    public static RecipeComparison Compare(ProductionScenario scenario, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        var pending = new Stack<Dictionary<string, string>>();
        pending.Push(new());
        var routes = new List<RecipeRoute>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        int infeasible = 0;
        bool current = true;

        while (pending.TryPop(out Dictionary<string, string>? fixedChoices))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var choices = new Dictionary<string, string>(scenario.RecipeChoices, StringComparer.Ordinal);
            foreach (var (item, recipe) in fixedChoices)
            {
                choices[item] = recipe;
            }

            try
            {
                ProductionChainBalance balance = ProductionChainCalculator.Calculate(scenario with { RecipeChoices = choices },
                    (item, selected) =>
                    {
                        if (fixedChoices.ContainsKey(item))
                        {
                            return;
                        }

                        // Fix the preceding decisions for each sibling. Only visit items reached by
                        // that branch, including inputs introduced by an alternate recipe or spray.
                        foreach (RecipeDefinition recipe in ProductionCatalog.RecipesFor(item).Where(recipe => recipe.Id != selected))
                        {
                            pending.Push(new Dictionary<string, string>(fixedChoices, StringComparer.Ordinal) { [item] = recipe.Id });
                        }
                        fixedChoices.Add(item, selected);
                    });

                string signature = string.Join("|", balance.Steps.OrderBy(step => step.RecipeId, StringComparer.Ordinal)
                    .Select(step => step.RecipeId + "=" + step.CyclesPerMinute.ToString("G29", CultureInfo.InvariantCulture)));
                if (seen.Add(signature))
                {
                    routes.Add(new RecipeRoute(new ReadOnlyDictionary<string, string>(choices), balance, current));
                }
            }
            catch (Exception error) when (error is ArgumentException or InvalidOperationException or OverflowException)
            {
                infeasible++;
            }
            current = false;
        }

        return new RecipeComparison(routes.OrderByDescending(route => route.Balance.Energy.NetPowerMw).ToArray(), infeasible);
    }
}
