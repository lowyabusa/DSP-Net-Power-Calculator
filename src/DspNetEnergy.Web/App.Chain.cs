using DspNetEnergy.Core;
using static DspNetEnergy.Web.UiLanguage;
using static DspNetEnergy.Web.Display;

namespace DspNetEnergy.Web;

public partial class App
{
    private static readonly SprayMode[] Modes = [SprayMode.None, SprayMode.ExtraProducts, SprayMode.Speed];
    private string? ContextRecipe;
    private string? FuelRecipeId => Settings.SuppliedItems.Contains(Fuel.ItemId) ? null :
        Settings.RecipeChoices.GetValueOrDefault(Fuel.ItemId) ?? ProductionCatalog.RecipesFor(Fuel.ItemId).FirstOrDefault()?.Id;

    private static decimal? DefaultOilRate(RecipeDefinition recipe, string machineId) => recipe.Id == "crude-oil-seep"
        ? recipe.Outputs["crude-oil"] * ProductionCatalog.Machine(machineId).Speed / recipe.CycleSeconds : null;

    private static string MaterialTip(string id, MaterialBalance amount) =>
        $"{Name(id)} · {T("items/min", "Stück/min")}\n" +
        $"{T("Produced", "Produziert")} {Precise(amount.ProducedPerMinute)}\n" +
        $"{T("Supplied", "Bereitgestellt")} {Precise(amount.SuppliedPerMinute)}\n" +
        $"{T("Used in production", "Verarbeitet")} {Precise(amount.UsedInProductionPerMinute)}\n" +
        $"{T("Burned", "Verbrannt")} {Precise(amount.BurnedPerMinute)}\n" +
        $"{T("Surplus", "Überschuss")} {Precise(amount.SurplusPerMinute)}";

    private string PrimaryOutput(RecipeDefinition recipe)
    {
        var products = recipe.Outputs.Keys.Where(item => recipe.Outputs[item] > recipe.Inputs.GetValueOrDefault(item)).ToArray();
        return products.FirstOrDefault(item => Settings.RecipeChoices.GetValueOrDefault(item) == recipe.Id)
            ?? products.FirstOrDefault(item => ProductionCatalog.RecipesFor(item).FirstOrDefault()?.Id == recipe.Id)
            ?? products[0];
    }

    private void ChooseRecipe(string itemId, string? shownRecipeId)
    {
        IReadOnlyList<RecipeDefinition> recipes = ProductionCatalog.RecipesFor(itemId);
        string? explicitRecipe = Settings.RecipeChoices.GetValueOrDefault(itemId);
        bool supplied = Settings.SuppliedItems.Contains(itemId) || recipes.Count == 0;
        var options = recipes.Select(recipe => new Choice(recipe.Id, Name(recipe.Id),
            RecipeGroup(recipe.Kind), recipe.Id, RecipeTip(recipe),
            !supplied && (explicitRecipe ?? shownRecipeId ?? recipes[0].Id) == recipe.Id)).ToList();
        if (recipes.Count > 0)
        {
            options.Add(new Choice("@auto", T("Automatic", "Automatisch"), T("Calculation scope", "Bilanzgrenze"), itemId,
                $"{T("Default recipe", "Standardrezept")} · {Name(recipes[0].Id)}", explicitRecipe is null && !supplied));
        }

        options.Add(new Choice("@supplied", T("Supplied", "Bereitgestellt"), T("Calculation scope", "Bilanzgrenze"), itemId,
            T("Upstream costs for this material are excluded.", "Die vorgelagerten Kosten dieses Materials sind ausgeschlossen."), supplied));
        OpenPicker(Format("Recipe for {0}", "Rezept für {0}", Name(itemId)), options, choice => SetRecipeChoice(itemId, choice));
    }

    private Task SetRecipeChoice(string itemId, string choice)
    {
        var choices = new Dictionary<string, string>(Settings.RecipeChoices);
        var suppliedItems = new HashSet<string>(Settings.SuppliedItems);
        choices.Remove(itemId);
        suppliedItems.Remove(itemId);
        if (choice == "@supplied")
        {
            suppliedItems.Add(itemId);
        }
        else if (choice != "@auto")
        {
            choices[itemId] = choice;
            editedStep = choice;
        }

        return Update(Settings with { RecipeChoices = choices, SuppliedItems = suppliedItems });
    }

    private void ChooseMachine(RecipeDefinition recipe, string selected)
    {
        var options = recipe.MachineIds.Select(id => new Choice(id, Name(id), T("Production facilities", "Produktionsgebäude"), id,
            MachineTip(id), id == selected)).ToArray();
        OpenPicker(Format("Machine for {0}", "Maschine für {0}", Name(recipe.Id)), options,
            choice => ChangeStep(recipe.Id, old => old with { MachineId = choice }));
    }


    private void ChooseSprayMode(RecipeDefinition recipe)
    {
        var options = Modes.Select(mode =>
        {
            (string name, string glyph, string? reason) = SprayChoice(recipe, mode);
            bool selected = (Settings.Tier is null ? SprayMode.None : Settings.Steps.GetValueOrDefault(recipe.Id)?.Spray ?? SprayMode.None) == mode;
            return new Choice(mode.ToString(), name, T("Spray mode", "Sprühmodus"), null, reason ?? T("Apply to this step", "Für diesen Schritt anwenden"),
                selected, reason is null, glyph);
        }).ToArray();
        OpenPicker(Format("Proliferator for {0}", "Proliferator für {0}", Name(recipe.Id)), options,
            choice => ChangeStep(recipe.Id, old => old with { Spray = Enum.Parse<SprayMode>(choice) }));
    }

    private (string Name, string Glyph, string? Reason) SprayChoice(RecipeDefinition recipe, SprayMode mode)
    {
        if (mode == SprayMode.None)
        {
            return (T("No spray", "Ohne Spray"), "∅", null);
        }

        bool allowed = mode == SprayMode.ExtraProducts ? recipe.AllowsExtraProducts : recipe.AllowsSpeed;
        string? reason = !allowed ? T("This recipe does not support this mode.", "Dieser Modus ist für das Rezept unzulässig.")
            : Settings.Tier is null ? T("Select a Proliferator tier first.", "Wähle zuerst eine Proliferator-Stufe.") : null;
        return mode == SprayMode.ExtraProducts ? (T("Extra Products", "Zusatzprodukte"), "+", reason) : (T("Production Speedup", "Produktionsbeschleunigung"), "»", reason);
    }

    private Task ChangeStep(string recipeId, Func<ProductionStepSettings, ProductionStepSettings> change)
    {
        var steps = new Dictionary<string, ProductionStepSettings>(Settings.Steps);
        steps[recipeId] = change(steps.GetValueOrDefault(recipeId) ?? new ProductionStepSettings());
        editedStep = recipeId;
        return Update(Settings with { Steps = steps });
    }

    private static string MachineTip(string machineId)
    {
        MachineDefinition machine = ProductionCatalog.Machine(machineId);
        return $"{Name(machineId)}\n{T("Speed", "Tempo")} ×{Amount(machine.Speed)}\n" +
            $"{T("Working power", "Arbeitsleistung")} {Precise(machine.WorkingPowerMw)} MW · " +
            $"{T("Idle power", "Leerlauf")} {Precise(machine.IdlePowerMw)} MW";
    }

    private static string RecipeTip(RecipeDefinition recipe) => $"{Name(recipe.Id)}\n" +
        $"{Describe(recipe.Inputs)} → {Describe(recipe.Outputs)}\n{Amount(recipe.CycleSeconds)} {T("s per base cycle", "s je Grundzyklus")}";

    private static string Describe(IReadOnlyDictionary<string, decimal> materials) => materials.Count == 0 ? T("Source", "Quelle")
        : string.Join(" + ", materials.Select(entry => $"{Precise(entry.Value)} {Name(entry.Key)}"));

    private static string RecipeGroup(RecipeKind kind) => kind switch
    {
        RecipeKind.Extraction => T("Resource extraction", "Rohstoffgewinnung"),
        RecipeKind.PhotonCapture => T("Dyson Sphere power", "Dyson-Energie"),
        _ => T("Processing", "Verarbeitung"),
    };
}
