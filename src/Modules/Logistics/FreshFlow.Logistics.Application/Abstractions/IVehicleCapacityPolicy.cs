namespace FreshFlow.Logistics.Application.Abstractions;

public interface IVehicleCapacityPolicy
{
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
}
