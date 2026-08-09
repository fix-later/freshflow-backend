using FreshFlow.Logistics.Application.Dtos;

namespace FreshFlow.Logistics.Application.Common;

public static class ShipmentEstimator
{
    public static ShipmentEstimateDto Estimate(
        Guid orderId,
        IReadOnlyList<OrderPackingLine> lines,
        decimal tareKg,
        Guid? vehicleId,
        decimal? vehicleCapacityKg)
    {
        var lineDtos = new List<ShipmentLineDto>();
        var missing = new List<string>();
        var totalBoxes = 0;
        var totalLoadKg = 0m;

        foreach (var line in lines)
        {
            if (line.CapacityKg is not { } capacityKg || capacityKg <= 0)
            {
                missing.Add(line.ProductName);
                continue;
            }

            var boxes = (int)Math.Ceiling(line.Quantity / capacityKg);
            var loadKg = line.Quantity + boxes * tareKg;
            totalBoxes += boxes;
            totalLoadKg += loadKg;
            lineDtos.Add(new ShipmentLineDto(
                line.ProductName, line.Quantity, capacityKg, boxes, loadKg));
        }

        bool? fitsVehicle = vehicleCapacityKg is { } capacity
            ? totalLoadKg <= capacity
            : null;

        return new ShipmentEstimateDto(
            orderId,
            totalBoxes,
            totalLoadKg,
            tareKg,
            vehicleId,
            vehicleCapacityKg,
            fitsVehicle,
            lineDtos.AsReadOnly(),
            missing.AsReadOnly());
    }
}
