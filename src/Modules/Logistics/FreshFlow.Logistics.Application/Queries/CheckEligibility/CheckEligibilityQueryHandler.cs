using FreshFlow.Logistics.Application.Abstractions;
using FreshFlow.Logistics.Application.Dtos;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Logistics.Application.Queries.CheckEligibility;

internal sealed class CheckEligibilityQueryHandler(
    IDeliveryRouteRepository routes,
    IVehicleRepository vehicles,
    IDriverReader drivers,
    IVehicleCapacityPolicy capacityPolicy)
    : IRequestHandler<CheckEligibilityQuery, Result<EligibilityResultDto>>
{
    public async Task<Result<EligibilityResultDto>> Handle(CheckEligibilityQuery request, CancellationToken ct)
    {
        var route = await routes.FindByIdAsync(request.RouteId, ct);
        if (route is null)
            return Result<EligibilityResultDto>.Failure(Error.NotFound("DELIVERY_ROUTE", request.RouteId));

        var reasons = new List<string>();

        var vehicle = await vehicles.FindByIdAsync(request.VehicleId, ct);
        if (vehicle is null)
        {
            reasons.Add("VEHICLE_NOT_FOUND");
        }
        else
        {
            if (vehicle.DeletedAt is not null)
                reasons.Add("VEHICLE_INACTIVE");

            if (!vehicle.IsAvailable)
                reasons.Add("VEHICLE_UNAVAILABLE");

            if (route.Stops.Count > capacityPolicy.MaxStopsPerVehicle)
                reasons.Add("VEHICLE_CAPACITY_EXCEEDED");

            if (await routes.ExistsOtherRouteForVehicleOnDateAsync(
                    request.VehicleId,
                    route.ServiceDate,
                    request.RouteId,
                    ct))
            {
                reasons.Add("VEHICLE_DOUBLE_BOOKED");
            }
        }

        if (request.DriverUserId is { } driverUserId)
        {
            var driver = await drivers.FindByUserIdAsync(driverUserId, ct);
            if (driver is null)
            {
                reasons.Add("DRIVER_NOT_FOUND");
            }
            else
            {
                if (!string.Equals(driver.RoleName, "driver", StringComparison.OrdinalIgnoreCase))
                    reasons.Add("DRIVER_NOT_DRIVER_ROLE");

                if (!driver.IsActive)
                    reasons.Add("DRIVER_INACTIVE");
            }
        }

        return Result<EligibilityResultDto>.Success(
            new EligibilityResultDto(reasons.Count == 0, reasons.AsReadOnly()));
    }
}
