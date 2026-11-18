namespace DspNetEnergy.Core;

internal sealed class RecipeResolver(ProductionScenario scenario, ProliferatorRule? rule,
    Action<string, string>? onRecipeChosen = null)
{
    private readonly HashSet<string> resolvedMaterials = new();
    private readonly Dictionary<string, ProductionActivity> recipes = new();
    private readonly Dictionary<string, ProductionActivity> supplies = new();

    internal ProductionActivity[] Resolve(string fuel, bool spraysFuel)
    {
        ValidateSelections();
        IncludeMaterial(fuel);
        if (spraysFuel && rule is not null)
        {
            IncludeMaterial(rule.ItemId);
        }

        return recipes.Values.Concat(supplies.Values).ToArray();
    }

    private void ValidateSelections()
    {
        foreach (var (item, recipeId) in scenario.RecipeChoices)
        {
            if (!GameCatalog.Recipes.TryGetValue(recipeId, out Recipe? recipe) || !recipe.Produces(item) ||
                scenario.SuppliedItems.Contains(item))
            {
                throw new ArgumentException($"Invalid or conflicting recipe choice '{recipeId}' for '{item}'.");
            }
        }

        foreach (string item in scenario.SuppliedItems)
        {
            if (!GameCatalog.Items.ContainsKey(item))
            {
                throw new ArgumentException($"Unknown supplied item '{item}'.");
            }
        }

        foreach (string recipeId in scenario.Steps.Keys)
        {
            if (!GameCatalog.Recipes.ContainsKey(recipeId))
            {
                throw new ArgumentException($"Unknown configured recipe '{recipeId}'.");
            }
        }
    }

    private void IncludeMaterial(string item)
    {
        if (!resolvedMaterials.Add(item))
        {
            return;
        }

        string? recipeId = scenario.SuppliedItems.Contains(item) ? null : ChooseRecipe(item);
        if (recipeId is null)
        {
            supplies.Add(item, ProductionActivity.Supply(item));
            return;
        }
        onRecipeChosen?.Invoke(item, recipeId);

        if (recipes.TryGetValue(recipeId, out ProductionActivity? existing))
        {
            existing.PrimaryOutputs.Add(item);
            return;
        }

        Recipe recipe = GameCatalog.Recipes[recipeId];
        var settings = scenario.Steps.GetValueOrDefault(recipeId) ?? new ProductionStepSettings();
        var activity = ProductionActivity.Create(recipeId, recipe, settings, rule);
        activity.PrimaryOutputs.Add(item);
        recipes.Add(recipeId, activity);
        foreach (var (input, amount) in activity.Inputs)
        {
            if (amount > activity.Outputs.GetValueOrDefault(input))
            {
                IncludeMaterial(input);
            }
        }

        if (activity.SpraysPerCycle > 0m && rule is not null)
        {
            IncludeMaterial(rule.ItemId);
        }
    }

    private string? ChooseRecipe(string item)
    {
        if (scenario.RecipeChoices.TryGetValue(item, out string? choice))
        {
            return choice;
        }

        return ProductionCatalog.RecipesFor(item).FirstOrDefault()?.Id;
    }
}
