using System.Globalization;
using FreshFlow.Logistics.Application.Abstractions;
using Microsoft.Extensions.Configuration;

namespace FreshFlow.Logistics.Infrastructure.Configuration;

internal sealed class VehicleCapacityPolicy : IVehicleCapacityPolicy
{
    public VehicleCapacityPolicy(IConfiguration config)
    {
        MaxStopsPerVehicle = int.TryParse(config["Logistics:MaxStopsPerVehicle"], out var value) && value > 0
            ? value
            : 20;

        BoxTareKg = decimal.TryParse(
            config["Logistics:Box:TareKg"],
            NumberStyles.Number,
            CultureInfo.InvariantCulture,
            out var tareKg) && tareKg > 0
            ? tareKg
            : 2m;

        CapacityUtilizationPercent = ReadDecimal(config, "Logistics:CapacityUtilizationPercent", 90m, 1m, 100m);
        MatrixBatchSize = ReadInt(config, "Logistics:Routing:MatrixBatchSize", 10, 1, 25);
        MatrixCacheEnabled = config.GetValue("Logistics:Routing:MatrixCache:Enabled", true);
        MatrixCacheMaxAgeDays = ReadClampedInt(config, "Logistics:Routing:MatrixCache:MaxAgeDays", 30, 1, 3650);
        SolverTimeLimitSeconds = ReadInt(config, "Logistics:Routing:SolverTimeLimitSeconds", 3, 1, 120);
        StartHour = ReadInt(config, "Logistics:Routing:StartHour", 6, 0, 23);
        ServiceTimeMinutes = ReadInt(config, "Logistics:Routing:ServiceTimeMinutes", 10, 0, 240);
        CostPerKm = ReadDecimal(config, "Logistics:Routing:CostPerKm", 5000m, 0m, decimal.MaxValue);
    }

    public int MaxStopsPerVehicle { get; }
    public decimal BoxTareKg { get; }
    public decimal CapacityUtilizationPercent { get; }
    public int MatrixBatchSize { get; }
    public bool MatrixCacheEnabled { get; }
    public int MatrixCacheMaxAgeDays { get; }
    public int SolverTimeLimitSeconds { get; }
    public int StartHour { get; }
    public int ServiceTimeMinutes { get; }
    public decimal CostPerKm { get; }

    private static int ReadInt(IConfiguration config, string key, int fallback, int min, int max) =>
        int.TryParse(config[key], out var value) && value >= min && value <= max ? value : fallback;

    private static int ReadClampedInt(IConfiguration config, string key, int fallback, int min, int max) =>
        int.TryParse(config[key], out var value) ? Math.Clamp(value, min, max) : fallback;

    private static decimal ReadDecimal(
        IConfiguration config, string key, decimal fallback, decimal min, decimal max) =>
        decimal.TryParse(config[key], NumberStyles.Number, CultureInfo.InvariantCulture, out var value)
        && value >= min && value <= max ? value : fallback;
}
