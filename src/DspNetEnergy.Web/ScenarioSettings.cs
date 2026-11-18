using static DspNetEnergy.Web.UiLanguage;
using System.Text.Json;
using System.Text.Json.Serialization;
using DspNetEnergy.Core;

namespace DspNetEnergy.Web;

internal sealed record ScenarioSettings
{
    public int SchemaVersion { get; init; } = 1;
    [JsonRequired] public FuelKind Fuel { get; init; } = FuelKind.Coal;
    [JsonRequired] public decimal Rate { get; init; } = 120m;
    public int? GeneratorCount { get; init; }
    public decimal? InfrastructureMw { get; init; }
    public LogisticsSettings? Logistics { get; init; }
    public ProliferatorTier? Tier { get; init; }
    public bool SprayFuel { get; init; } = true;
    public bool SelfSpray { get; init; }
    public int? CoaterCount { get; init; }
    public decimal CoaterRate { get; init; } = 1800m;
    public Dictionary<string, string> RecipeChoices { get; init; } = new();
    public Dictionary<string, ProductionStepSettings> Steps { get; init; } = new();
    public HashSet<string> SuppliedItems { get; init; } = new();

    internal ProductionScenario ToScenario(ProliferatorTier? tier) => new(Fuel, Rate, GeneratorCount)
    {
        OtherInfrastructureMw = InfrastructureMw,
        Logistics = Logistics,
        RecipeChoices = RecipeChoices,
        Steps = tier.HasValue ? Steps : Steps.ToDictionary(entry => entry.Key,
            entry => entry.Value with { Spray = SprayMode.None }),
        SuppliedItems = SuppliedItems,
        Proliferation = tier is ProliferatorTier activeTier
            ? new ProliferationOptions(activeTier, SprayFuel, SelfSpray,
                CoaterCount, CoaterRate)
            : null,
    };
}

internal static class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) },
    };

    internal static ScenarioSettings Read(string json)
    {
        ScenarioSettings scenario = JsonSerializer.Deserialize<ScenarioSettings>(json, JsonOptions)
            ?? throw new JsonException(T("The scenario file is empty.", "Die Szenariodatei ist leer."));
        if (scenario.SchemaVersion != 1)
        {
            throw new JsonException(Format("Scenario format {0} is not supported.",
                "Szenarioformat {0} wird nicht unterstützt.", scenario.SchemaVersion));
        }
        if (scenario.CoaterCount is < 0 || scenario.CoaterRate <= 0m ||
            scenario.Steps.Values.Any(step => step is null) || scenario.SuppliedItems.Contains(null!))
        {
            throw new JsonException(T("Invalid Spray Coater or production settings.", "Ungültige Coater- oder Produktionsparameter."));
        }

        foreach (var (id, step) in scenario.Steps)
        {
            RecipeDefinition recipe;
            try
            {
                recipe = ProductionCatalog.Recipe(id);
            }
            catch (KeyNotFoundException)
            {
                throw new JsonException(Format("Unknown recipe '{0}'.", "Unbekanntes Rezept „{0}“.", id));
            }

            if ((step.MachineId is not null && !recipe.MachineIds.Contains(step.MachineId)) ||
                step.MachineCount is < 0 || step.MaximumUtilization is <= 0m or > 1m || step.RateMultiplier <= 0m ||
                step.WorkingPowerMw is < 0m || step.IdlePowerMw is < 0m ||
                (step.Spray == SprayMode.Speed && !recipe.AllowsSpeed) ||
                (step.Spray == SprayMode.ExtraProducts && !recipe.AllowsExtraProducts))
            {
                throw new JsonException(Format("Invalid machine or spray setting for '{0}'.",
                    "Ungültige Maschinen- oder Sprüheinstellung für „{0}“.", id));
            }
        }

        EnergyCalculator.Calculate(scenario.ToScenario(scenario.Tier));
        return scenario;
    }

    internal static string Write(ScenarioSettings scenario) => JsonSerializer.Serialize(scenario, JsonOptions);
}
