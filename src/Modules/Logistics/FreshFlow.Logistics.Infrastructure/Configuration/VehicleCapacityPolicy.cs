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
    }

    public int MaxStopsPerVehicle { get; }
}
