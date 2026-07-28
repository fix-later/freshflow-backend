using FreshFlow.Logistics.Application.Abstractions;
using FreshFlow.Logistics.Application.Dtos;
using FreshFlow.Logistics.Application.Mappings;
using FreshFlow.Logistics.Application.Queries.CheckEligibility;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Logistics.Application.Commands.AssignVehicle;

internal sealed class AssignVehicleCommandHandler(
    IDeliveryRouteRepository routes,
    ISender sender)
    : IRequestHandler<AssignVehicleCommand, Result<RouteDto>>
{
    public async Task<Result<RouteDto>> Handle(AssignVehicleCommand request, CancellationToken ct)
    {
        // ponytail: dispatch is intentionally fleet-wide — any hub_staff may assign any vehicle to any
        // route. Topology today is one hub per market with a shared fleet, so per-hub scoping is a no-op.
        // If fleet segregation across hubs is ever needed, add Vehicle.HubId + a hub-assignment guard here
        // (see HubAccessChecker in the Hub module for the pattern) and scope by the vehicle's home hub.
        var route = await routes.FindByIdAsync(request.RouteId, ct);
        if (route is null)
            return Result<RouteDto>.Failure(Error.NotFound("DELIVERY_ROUTE", request.RouteId));

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

        try
        {
            route.Assign(request.VehicleId, request.DriverUserId);
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
