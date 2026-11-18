namespace DspNetEnergy.Core;

/// <summary>Power per installed unit at full load and at idle, in kW.</summary>
public sealed record LogisticsEquipment(int Count, decimal Utilization, decimal WorkingKw, decimal IdleKw)
{
    internal LogisticsPower Calculate()
    {
        if (Count < 0 || Utilization is < 0m or > 1m || WorkingKw < 0m || IdleKw < 0m)
        {
            throw new ArgumentException("Invalid logistics equipment count, utilization or power.");
        }

        decimal active = Count * Utilization;
        return new(active * WorkingKw / 1000m, (Count - active) * IdleKw / 1000m);
    }
}

/// <summary>Energy charged to the factory per completed trip, including the return leg if applicable.</summary>
public sealed record LogisticsTrips(decimal TripsPerMinute, decimal KilojoulesPerTrip)
{
    internal LogisticsPower Calculate()
    {
        if (TripsPerMinute < 0m || KilojoulesPerTrip < 0m)
        {
            throw new ArgumentException("Invalid logistics trip rate or energy.");
        }

        return new(TripsPerMinute * KilojoulesPerTrip / (1000m * EnergyCalculator.SecondsPerMinute), 0m);
    }
}

/// <summary>Null components mark unconfigured costs. Station power excludes vehicle charging.</summary>
public sealed record LogisticsSettings(LogisticsEquipment? Sorters = null, LogisticsEquipment? Stations = null,
    LogisticsTrips? Drones = null, LogisticsTrips? Vessels = null)
{
    internal LogisticsBalance Calculate() => new(Sorters?.Calculate(), Stations?.Calculate(),
        Drones?.Calculate(), Vessels?.Calculate());
}

public sealed record LogisticsPower(decimal WorkingMw, decimal IdleMw);

public sealed record LogisticsBalance(LogisticsPower? Sorters, LogisticsPower? Stations,
    LogisticsPower? Drones, LogisticsPower? Vessels)
{
    internal bool HasExclusions => Sorters is null || Stations is null || Drones is null || Vessels is null;

    internal decimal WorkingMw => Components.Sum(power => power?.WorkingMw ?? 0m);
    internal decimal IdleMw => Components.Sum(power => power?.IdleMw ?? 0m);

    private LogisticsPower?[] Components => [Sorters, Stations, Drones, Vessels];
}
