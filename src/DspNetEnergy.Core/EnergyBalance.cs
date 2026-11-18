namespace DspNetEnergy.Core;

[Flags]
public enum ExcludedCosts
{
    None = 0,
    ResourceExtraction = 1,
    FuelProduction = 2,
    SortersAndLogistics = 4,
    SuppliedMaterials = 8,
}

public sealed record SmeltingBalance(
    int SmelterCount,
    decimal Utilization,
    decimal ConsumedCoalPerMinute,
    decimal UnusedCoalPerMinute,
    decimal GraphitePerMinute,
    decimal WorkingPowerMw,
    decimal IdlePowerMw);

public sealed record EnergyBalance(
    decimal AvailableFuelPerMinute,
    decimal BurnedFuelPerMinute,
    decimal FuelPowerMw,
    decimal PotentialElectricPowerMw,
    int GeneratorCount,
    decimal GeneratorCapacityMw,
    decimal GrossPowerMw,
    SmeltingBalance? Smelting,
    ExcludedCosts ExcludedCosts)
{
    internal ConsumptionBreakdown? ChainConsumption { get; init; }

    public decimal FuelEnergyMjPerItem { get; internal init; }

    public decimal UnburnedFuelPerMinute => AvailableFuelPerMinute - BurnedFuelPerMinute;
    public decimal TotalConsumptionMw => ChainConsumption?.TotalMw
        ?? (Smelting is null ? 0m : Smelting.WorkingPowerMw + Smelting.IdlePowerMw);
    public decimal NetPowerMw => GrossPowerMw - TotalConsumptionMw;
}
