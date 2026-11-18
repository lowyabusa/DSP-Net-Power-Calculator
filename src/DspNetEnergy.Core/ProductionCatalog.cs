using System.Collections.ObjectModel;

namespace DspNetEnergy.Core;

public enum RecipeKind { Manufacturing, Extraction, PhotonCapture, Fractionation }

public sealed record FuelDefinition(FuelKind Kind, string ItemId, decimal EnergyMj, string GeneratorIconId,
    decimal GeneratorCapacityMw, decimal GeneratorEfficiency);

public sealed record MachineDefinition(string Id, decimal Speed, decimal WorkingPowerMw, decimal IdlePowerMw);

public sealed record ProliferatorDefinition(string ItemId, decimal ExtraProductsMultiplier, decimal SpeedMultiplier,
    decimal WorkingPowerMultiplier, int SpraysPerUnit, int? SelfSprayedCapacity);

public sealed record RecipeDefinition(string Id, RecipeKind Kind, decimal CycleSeconds,
    IReadOnlyDictionary<string, decimal> Inputs, IReadOnlyDictionary<string, decimal> Outputs,
    IReadOnlyList<string> MachineIds, bool AllowsSpeed, bool AllowsExtraProducts);

/// <summary>Read-only game metadata for constructing scenarios.</summary>
public static class ProductionCatalog
{
    public static IReadOnlyList<FuelDefinition> Fuels { get; } = Array.AsReadOnly(Enum.GetValues<FuelKind>().Select(kind =>
    {
        string id = GameCatalog.GetFuelId(kind);
        Item fuel = GameCatalog.Items[id];
        Generator generator = GameCatalog.GetGenerator(fuel);
        string generatorIcon = fuel.Generator switch
        {
            GeneratorKind.Thermal => "thermal-power-plant",
            GeneratorKind.MiniFusion => "mini-fusion-power-plant",
            GeneratorKind.ArtificialStar => "artificial-star",
            _ => throw new InvalidDataException($"Missing generator for '{id}'."),
        };
        return new FuelDefinition(kind, id, fuel.EnergyMj, generatorIcon,
            generator.MaxPowerMw * fuel.GeneratorCapacityMultiplier, generator.Efficiency);
    }).ToArray());

    private static readonly IReadOnlyDictionary<string, RecipeDefinition> Recipes = GameCatalog.Recipes.ToDictionary(
        entry => entry.Key, entry => DescribeRecipe(entry.Key, entry.Value));

    public static RecipeDefinition Recipe(string id) => Recipes[id];

    public static ProliferatorDefinition Proliferator(ProliferatorTier tier)
    {
        ProliferatorRule rule = GameCatalog.Proliferators[tier];
        return new ProliferatorDefinition(rule.ItemId, rule.ExtraProductsMultiplier, rule.SpeedMultiplier,
            rule.WorkingPowerMultiplier, rule.SpraysPerUnit, rule.SelfSprayedCapacity);
    }

    public static MachineDefinition Machine(string id)
    {
        Machine machine = GameCatalog.Machines[id];
        return new MachineDefinition(id, machine.Speed, machine.WorkingPowerMw, machine.IdlePowerMw);
    }

    public static IReadOnlyList<RecipeDefinition> RecipesFor(string itemId) => GameCatalog.Recipes
        .Where(entry => entry.Value.Produces(itemId))
        .OrderBy(entry => entry.Value.Priority)
        .ThenByDescending(entry => entry.Key == itemId)
        .ThenBy(entry => entry.Key, StringComparer.Ordinal)
        .Select(entry => Recipes[entry.Key]).ToArray();

    private static RecipeDefinition DescribeRecipe(string id, Recipe recipe) => new(id, recipe.Kind, recipe.CycleSeconds,
        new ReadOnlyDictionary<string, decimal>(recipe.Inputs), new ReadOnlyDictionary<string, decimal>(recipe.Outputs),
        Array.AsReadOnly(recipe.Machines), recipe.AllowsSpray(SprayMode.Speed), recipe.AllowsSpray(SprayMode.ExtraProducts));
}
