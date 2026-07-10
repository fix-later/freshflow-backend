namespace FreshFlow.Logistics.Application.Abstractions;

public interface IVehicleCapacityPolicy
{
    public int MaxStopsPerVehicle { get; }
}
