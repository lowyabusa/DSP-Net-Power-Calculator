using System.Text.Json;
using System.Text.Json.Serialization;

namespace DspNetEnergy.Core;

internal enum GeneratorKind { Thermal, MiniFusion, ArtificialStar }

internal sealed record Item(string Name, FuelKind? Fuel = null, decimal EnergyMj = 0m,
    GeneratorKind? Generator = null, decimal GeneratorCapacityMultiplier = 1m, decimal ExternalEnergyMj = 0m);
internal sealed record Generator(decimal Efficiency, decimal MaxPowerMw, bool SprayBoostsCapacityOnly = false);
internal sealed record Machine(decimal Speed, decimal WorkingPowerMw, decimal IdlePowerMw);
internal sealed record Recipe(decimal CycleSeconds, Dictionary<string, decimal> Inputs,
    Dictionary<string, decimal> Outputs, string[] Machines, RecipeKind Kind = RecipeKind.Manufacturing,
    bool ExtraProducts = false, int Priority = 0)
{
    internal bool Produces(string item) => Outputs.GetValueOrDefault(item) > Inputs.GetValueOrDefault(item);

    internal bool AllowsSpray(SprayMode mode) => mode switch
    {
        SprayMode.None => true,
        SprayMode.ExtraProducts => Inputs.Count > 0 && ExtraProducts,
        SprayMode.Speed => Inputs.Count > 0,
        _ => false,
    };
}
internal sealed record ProliferatorRule(string ItemId, decimal ExtraProductsMultiplier,
    decimal SpeedMultiplier, decimal WorkingPowerMultiplier, int SpraysPerUnit, int? SelfSprayedCapacity = null);

internal static class GameCatalog
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) },
        RespectRequiredConstructorParameters = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    internal static readonly IReadOnlyDictionary<string, Item> Items = Load<string, Item>("items");
    internal static readonly IReadOnlyDictionary<string, Recipe> Recipes = Load<string, Recipe>("recipes");
    internal static readonly IReadOnlyDictionary<string, Machine> Machines = Load<string, Machine>("machines");
    internal static readonly IReadOnlyDictionary<ProliferatorTier, ProliferatorRule> Proliferators =
        Load<ProliferatorTier, ProliferatorRule>("proliferators");
    private static readonly Dictionary<GeneratorKind, Generator> Generators = Load<GeneratorKind, Generator>("generators");
    private static readonly Dictionary<FuelKind, string> FuelIds = Items.Where(entry => entry.Value.Fuel.HasValue)
        .ToDictionary(entry => entry.Value.Fuel!.Value, entry => entry.Key);

    static GameCatalog()
    {
        foreach (var (id, recipe) in Recipes)
        {
            if (recipe.CycleSeconds <= 0m || recipe.Outputs.Count == 0 || recipe.Machines.Length == 0 ||
                recipe.Inputs.Concat(recipe.Outputs).Any(entry => entry.Value <= 0m || !Items.ContainsKey(entry.Key)) ||
                recipe.Machines.Any(machine => !Machines.ContainsKey(machine)))
            {
                throw new InvalidDataException($"Invalid recipe data for '{id}'.");
            }
        }

        foreach (var (id, machine) in Machines)
        {
            if (machine.Speed <= 0m || machine.WorkingPowerMw < 0m || machine.IdlePowerMw < 0m)
            {
                throw new InvalidDataException($"Invalid machine data for '{id}'.");
            }
        }

        foreach (var (kind, id) in FuelIds)
        {
            Item item = Items[id];
            if (item.EnergyMj <= 0m || item.GeneratorCapacityMultiplier <= 0m ||
                item.Generator is not GeneratorKind generator || !Generators.ContainsKey(generator))
            {
                throw new InvalidDataException($"Invalid fuel data for '{kind}'.");
            }
        }
    }

    internal static string GetFuelId(FuelKind kind) => FuelIds.TryGetValue(kind, out string? id)
        ? id
        : throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown fuel.");

    internal static Generator GetGenerator(Item fuel) => Generators[fuel.Generator!.Value];

    private static Dictionary<TKey, TValue> Load<TKey, TValue>(string dataset) where TKey : notnull
    {
        var assembly = typeof(GameCatalog).Assembly;
        using Stream stream = assembly.GetManifestResourceStream($"DspNetEnergy.Core.GameData.{dataset}.json")
            ?? throw new InvalidDataException($"Missing game dataset: {dataset}.");
        return JsonSerializer.Deserialize<Dictionary<TKey, TValue>>(stream, JsonOptions)
            ?? throw new InvalidDataException($"Empty game dataset: {dataset}.");
    }
}
