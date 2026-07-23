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
    }

    public int MaxStopsPerVehicle { get; }
    public decimal BoxTareKg { get; }
}
