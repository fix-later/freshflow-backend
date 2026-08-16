using FreshFlow.Logistics.Application.Abstractions;
using FreshFlow.Logistics.Application.Dtos;
using FreshFlow.Logistics.Application.Mappings;
using FreshFlow.Logistics.Application.Queries.CheckEligibility;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Logistics.Application.Commands.AssignVehicle;

internal sealed class AssignVehicleCommandHandler(
    IDeliveryRouteRepository routes,
    IVehicleRepository vehicles,
    ISender sender)
    : IRequestHandler<AssignVehicleCommand, Result<RouteDto>>
{
    public async Task<Result<RouteDto>> Handle(AssignVehicleCommand request, CancellationToken ct)
    {
        var route = await routes.FindByIdAsync(request.RouteId, ct);
        if (route is null)
            return Result<RouteDto>.Failure(Error.NotFound("DELIVERY_ROUTE", request.RouteId));

        var vehicle = await vehicles.FindByIdAsync(request.VehicleId, ct);
        if (vehicle is null)
            return Result<RouteDto>.Failure(Error.NotFound("VEHICLE", request.VehicleId));

        if (vehicle.HubId is null)
        {
            return Result<RouteDto>.Failure(
                Error.Validation("VEHICLE_HUB_UNASSIGNED", "Vehicle chưa gán hub."));
        }

        if (route.HubId is not null && vehicle.HubId != route.HubId)
        {
            return Result<RouteDto>.Failure(
                Error.Validation("VEHICLE_HUB_MISMATCH", "Xe không thuộc hub của route."));
        }

        var eligibilityResult = await sender.Send(
            new CheckEligibilityQuery(request.RouteId, request.VehicleId, request.DriverUserId),
            ct);
        if (!eligibilityResult.IsSuccess)
            return Result<RouteDto>.Failure(eligibilityResult.Error);

        if (!eligibilityResult.Value.IsEligible)
        {
            var reasons = eligibilityResult.Value.Reasons;
            if (reasons.Contains("VEHICLE_WEIGHT_CAPACITY_EXCEEDED"))
            {
                return Result<RouteDto>.Failure(Error.Validation(
                    "VALIDATION_ERROR",
                    "VEHICLE_WEIGHT_CAPACITY_EXCEEDED: Route load exceeds vehicle capacity."));
            }

            var isConflict = reasons.Contains("VEHICLE_DOUBLE_BOOKED") || reasons.Contains("VEHICLE_UNAVAILABLE");
            return isConflict
                ? Result<RouteDto>.Failure(Error.Conflict(
                    "VEHICLE_NOT_AVAILABLE",
                    $"Vehicle is not available: {string.Join(", ", reasons)}"))
                : Result<RouteDto>.Failure(Error.Validation(
                    "VEHICLE_NOT_ELIGIBLE",
                    $"Vehicle/driver not eligible: {string.Join(", ", reasons)}"));
        }

        if (request.DriverUserId is null)
        {
            return Result<RouteDto>.Failure(Error.Validation(
                "DRIVER_REQUIRED", "DriverUserId is required."));
        }

        try
        {
            route.Assign(request.VehicleId, request.DriverUserId.Value);
        }
        catch (InvalidOperationException ex)
        {
            return Result<RouteDto>.Failure(Error.Conflict("ROUTE_INVALID_TRANSITION", ex.Message));
        }

        var saved = await routes.SaveAssignmentAsync(ct);
        if (!saved)
        {
            return Result<RouteDto>.Failure(Error.Conflict(
                "VEHICLE_NOT_AVAILABLE",
                "Vehicle is already assigned to another route on this service date."));
        }

        return Result<RouteDto>.Success(route.ToDto());
    }
}
