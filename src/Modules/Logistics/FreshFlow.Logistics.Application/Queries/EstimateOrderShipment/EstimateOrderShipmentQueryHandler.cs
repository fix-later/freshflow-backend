using FreshFlow.Logistics.Application.Abstractions;
using FreshFlow.Logistics.Application.Common;
using FreshFlow.Logistics.Application.Dtos;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Logistics.Application.Queries.EstimateOrderShipment;

internal sealed class EstimateOrderShipmentQueryHandler(
    IOrderStatusReader orders,
    IOrderPackingReader packing,
    IVehicleRepository vehicles,
    IVehicleCapacityPolicy policy)
    : IRequestHandler<EstimateOrderShipmentQuery, Result<ShipmentEstimateDto>>
{
    public async Task<Result<ShipmentEstimateDto>> Handle(
        EstimateOrderShipmentQuery request, CancellationToken ct)
    {
        var order = await orders.FindByIdAsync(request.OrderId, ct);
        if (order is null)
            return Result<ShipmentEstimateDto>.Failure(Error.NotFound("ORDER", request.OrderId));

        decimal? vehicleCapacityKg = null;
        if (request.VehicleId is Guid vehicleId)
        {
            var vehicle = await vehicles.FindByIdAsync(vehicleId, ct);
            if (vehicle is null || vehicle.DeletedAt is not null)
                return Result<ShipmentEstimateDto>.Failure(Error.NotFound("VEHICLE", vehicleId));

            vehicleCapacityKg = vehicle.CapacityKg;
        }

        var lines = await packing.GetLinesAsync(request.OrderId, ct);
        return Result<ShipmentEstimateDto>.Success(
            ShipmentEstimator.Estimate(
                request.OrderId,
                lines,
                policy.BoxTareKg,
                request.VehicleId,
                vehicleCapacityKg));
    }
}
