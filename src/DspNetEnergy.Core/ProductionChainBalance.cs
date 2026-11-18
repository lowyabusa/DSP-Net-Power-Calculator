namespace DspNetEnergy.Core;

public sealed record ConsumptionBreakdown(
    decimal ResourceExtractionMw,
    decimal FuelProductionMw,
    decimal ProliferatorProductionMw,
    decimal SprayAndLogisticsMw,
    decimal IdleMw)
{
    public decimal TotalMw => ResourceExtractionMw + FuelProductionMw + ProliferatorProductionMw
        + SprayAndLogisticsMw + IdleMw;
}

public sealed record ProductionStepBalance(
    string RecipeId,
    string MachineId,
    int MachineCount,
    decimal Utilization,
    decimal CyclesPerMinute,
    decimal WorkingPowerMw,
    decimal IdlePowerMw,
    IReadOnlyDictionary<string, decimal> InputsPerMinute,
    IReadOnlyDictionary<string, decimal> OutputsPerMinute);

public sealed record MaterialBalance(
    decimal ProducedPerMinute,
    decimal SuppliedPerMinute,
    decimal UsedInProductionPerMinute,
    decimal BurnedPerMinute)
{
    public decimal SurplusPerMinute => ProducedPerMinute + SuppliedPerMinute - UsedInProductionPerMinute - BurnedPerMinute;
}

public sealed record SprayBalance(
    int UsableSpraysPerUnit,
    decimal ProductionSpraysPerMinute,
    decimal FuelSpraysPerMinute,
    decimal SelfSpraysPerMinute,
    decimal ProliferatorUnitsPerMinute,
    int CoaterCount,
    decimal CoaterUtilization,
    decimal CoaterWorkingPowerMw,
    decimal CoaterIdlePowerMw);

public sealed record ProductionChainBalance(
    EnergyBalance Energy,
    ConsumptionBreakdown Consumption,
    IReadOnlyList<ProductionStepBalance> Steps,
    IReadOnlyDictionary<string, MaterialBalance> Materials,
    SprayBalance? Spray,
    decimal ExternalDysonPowerMw)
{
    public LogisticsBalance? Logistics { get; init; }
}
