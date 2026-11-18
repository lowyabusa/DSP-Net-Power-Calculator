namespace DspNetEnergy.Core;

public enum FuelKind
{
    Coal,
    EnergeticGraphite,
    DeuteronFuelRod,
    AntimatterFuelRod,
    StrangeAnnihilationFuelRod,
    Log,
    PlantFuel,
    FireIce,
    Diamond,
    CrudeOil,
    RefinedOil,
    Hydrogen,
    Deuterium,
    HydrogenFuelRod,
    Graphene,
    CarbonNanotube,
    OrganicCrystal,
    CombustibleUnit,
    ExplosiveUnit,
    CrystalExplosiveUnit,
    ProliferatorMkI,
    ProliferatorMkII,
    ProliferatorMkIII,
    EnergyShard,
}

/// <summary>Combustion of fuel already supplied at the generator input.</summary>
public sealed record DirectCombustionScenario(
    FuelKind Fuel,
    decimal AvailableFuelPerMinute,
    int? GeneratorCount = null);

/// <summary>Smelting of supplied coal, followed by combustion of the produced graphite.</summary>
public sealed record CoalToGraphiteScenario(
    decimal AvailableCoalPerMinute,
    int? SmelterCount = null,
    int? GeneratorCount = null);
