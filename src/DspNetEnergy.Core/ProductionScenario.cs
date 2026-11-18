namespace DspNetEnergy.Core;

public enum ProliferatorTier { MkI, MkII, MkIII }
public enum SprayMode { None, ExtraProducts, Speed }

public sealed record ProliferationOptions(
    ProliferatorTier Tier,
    bool SprayFuel = false,
    bool SelfSpray = false,
    int? CoaterCount = null,
    decimal ItemsPerMinutePerCoater = 1800m);

public sealed record ProductionStepSettings(
    string? MachineId = null,
    int? MachineCount = null,
    decimal MaximumUtilization = 1m,
    decimal RateMultiplier = 1m,
    SprayMode Spray = SprayMode.None,
    decimal? WorkingPowerMw = null,
    decimal? IdlePowerMw = null);

/// <summary>Stationary production to a target fuel throughput in items per minute.</summary>
public sealed record ProductionScenario(FuelKind Fuel, decimal FuelPerMinute, int? GeneratorCount = null)
{
    /// <summary>Recipe IDs by material ID. A recipe is selected once even if it provides multiple items.</summary>
    public IReadOnlyDictionary<string, string> RecipeChoices { get; init; } = new Dictionary<string, string>();
    public IReadOnlyDictionary<string, ProductionStepSettings> Steps { get; init; } =
        new Dictionary<string, ProductionStepSettings>();

    /// <summary>Materials available at the scenario boundary with their upstream costs explicitly excluded.</summary>
    public IReadOnlySet<string> SuppliedItems { get; init; } = new HashSet<string>();
    public ProliferationOptions? Proliferation { get; init; }

    /// <summary>Configured logistics beyond the spray coaters. Null uses the legacy infrastructure boundary.</summary>
    public LogisticsSettings? Logistics { get; init; }

    /// <summary>Legacy infrastructure total, or additional power beyond itemized logistics when configured.</summary>
    public decimal? OtherInfrastructureMw { get; init; }
}
