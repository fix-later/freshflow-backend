using FreshFlow.Logistics.Application.Abstractions;
using FreshFlow.Logistics.Application.Dtos;
using FreshFlow.Logistics.Application.Mappings;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Logistics.Application.Queries.GetRoutePlan;

internal sealed class GetRoutePlanQueryHandler(
    IRoutePlanRepository plans,
    IVehicleRepository vehicles,
    IVehicleCapacityPolicy settings)
    : IRequestHandler<GetRoutePlanQuery, Result<RoutePlanDto>>
{
    public async Task<Result<RoutePlanDto>> Handle(GetRoutePlanQuery request, CancellationToken ct)
    {
        var plan = await plans.FindByIdAsync(request.PlanId, ct);
        if (plan is null)
            return Result<RoutePlanDto>.Failure(Error.NotFound("ROUTE_PLAN", request.PlanId));
        var routes = await plans.GetRoutesAsync(plan.Id, ct);
        var mappedVehicles = new Dictionary<Guid, PlanningVehicle>();
        foreach (var vehicleId in routes.Select(x => x.SuggestedVehicleId).OfType<Guid>().Distinct())
        {
            var vehicle = await vehicles.FindByIdAsync(vehicleId, ct);
            if (vehicle is null)
                return Result<RoutePlanDto>.Failure(Error.NotFound("VEHICLE", vehicleId));
            mappedVehicles[vehicleId] = new PlanningVehicle(
                vehicle.Id, vehicle.PlateNumber, vehicle.VehicleType,
                Common.RoutePlanningInputBuilder.Profile(vehicle.VehicleType), vehicle.CapacityKg,
                vehicle.CapacityKg * settings.CapacityUtilizationPercent / 100m);
        }
        return Result<RoutePlanDto>.Success(plan.ToDto(routes, mappedVehicles));
    }
}
