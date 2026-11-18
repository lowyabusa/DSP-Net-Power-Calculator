using static DspNetEnergy.Desktop.UiLanguage;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using DspNetEnergy.Core;

namespace DspNetEnergy.Desktop;

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

    private static readonly string Pathname = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DspNetEnergy", "scenario.json");
    private static readonly string LanguagePath = Path.Combine(Path.GetDirectoryName(Pathname)!, "language.json");
    private static readonly string GameIconsPath = Path.Combine(Path.GetDirectoryName(Pathname)!, "game-icons.json");

    private static bool Transient => Environment.GetCommandLineArgs().Contains("--transient");

    internal static GameIconPreference LoadGameIcons()
    {
        if (Transient || !File.Exists(GameIconsPath))
        {
            return new();
        }

        try
        {
            return JsonSerializer.Deserialize<GameIconPreference>(File.ReadAllText(GameIconsPath), JsonOptions) ?? new();
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        {
            return new();
        }
    }

    internal static void SaveGameIcons(GameIconPreference value)
    {
        if (!Transient)
        {
            WriteJson(GameIconsPath, value);
        }
    }

    internal static AppLanguage LoadLanguage()
    {
        if (Transient || !File.Exists(LanguagePath))
        {
            return AppLanguage.English;
        }

        try
        {
            return JsonSerializer.Deserialize<string>(File.ReadAllText(LanguagePath), JsonOptions) switch
            {
                "de" => AppLanguage.German,
                "zh-CN" => AppLanguage.Chinese,
                _ => AppLanguage.English,
            };
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        {
            return AppLanguage.English;
        }
    }

    internal static void SaveLanguage(AppLanguage language)
    {
        if (!Transient)
        {
            WriteJson(LanguagePath, language switch
            {
                AppLanguage.German => "de",
                AppLanguage.Chinese => "zh-CN",
                _ => "en",
            });
        }
    }

    internal static ScenarioSettings Load(out string? message)
    {
        message = null;
        if (Transient || !File.Exists(Pathname))
        {
            return new ScenarioSettings();
        }

        try
        {
            return Read(Pathname);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException
            or ArgumentException or InvalidOperationException or OverflowException)
        {
            message = T("Could not load the saved scenario. Defaults are active.", "Das gespeicherte Szenario konnte nicht geladen werden. Standardwerte sind aktiv.");
            return new ScenarioSettings();
        }
    }

    internal static void Save(ScenarioSettings scenario)
    {
        if (!Transient)
        {
            Write(Pathname, scenario);
        }
    }

    internal static ScenarioSettings Read(string path)
    {
        ScenarioSettings scenario = JsonSerializer.Deserialize<ScenarioSettings>(File.ReadAllText(path), JsonOptions)
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

    internal static void Write(string path, ScenarioSettings scenario) => WriteJson(path, scenario);

    private static void WriteJson<TValue>(string path, TValue value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (FileStream stream = File.Open(temporary, FileMode.CreateNew))
            {
                JsonSerializer.Serialize(stream, value, JsonOptions);
            }
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            File.Delete(temporary);
        }
    }
}
