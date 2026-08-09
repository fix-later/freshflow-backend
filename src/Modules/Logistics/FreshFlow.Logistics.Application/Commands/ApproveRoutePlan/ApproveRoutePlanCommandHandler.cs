using FreshFlow.Logistics.Application.Abstractions;
using FreshFlow.Logistics.Application.Dtos;
using FreshFlow.Logistics.Application.Mappings;
using FreshFlow.Logistics.Domain.Entities;
using FreshFlow.Logistics.Domain.Enums;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Logistics.Application.Commands.ApproveRoutePlan;

internal sealed class ApproveRoutePlanCommandHandler(
    IRoutePlanRepository plans,
    IRoutePlanningInputBuilder inputs,
    IDeliveryRepository deliveries)
    : IRequestHandler<ApproveRoutePlanCommand, Result<RoutePlanDto>>
{
    public async Task<Result<RoutePlanDto>> Handle(ApproveRoutePlanCommand request, CancellationToken ct)
    {
        var plan = await plans.FindByIdAsync(request.PlanId, ct);
        if (plan is null)
            return Result<RoutePlanDto>.Failure(Error.NotFound("ROUTE_PLAN", request.PlanId));
        if (plan.Status != RoutePlanStatus.proposed)
            return Result<RoutePlanDto>.Failure(Error.Conflict(
                "ROUTE_PLAN_NOT_PROPOSED", "Only a proposed route plan can be approved."));

        var inputResult = await inputs.BuildAsync(plan.HubId, plan.ServiceDate, ct);
        if (!inputResult.IsSuccess)
        {
            plan.MarkStale();
            await plans.TrySaveChangesAsync(ct);
            return Result<RoutePlanDto>.Failure(Error.Conflict(
                "PLAN_STALE", "Route inputs are no longer complete or available."));
        }
        var input = inputResult.Value;
        if (input.InputRevision != plan.InputRevision)
        {
            plan.MarkStale();
            await plans.TrySaveChangesAsync(ct);
            return Result<RoutePlanDto>.Failure(Error.Conflict(
                "PLAN_STALE", "Orders, coordinates, fleet, or routing settings changed after planning."));
        }
        if (plan.Unassigned.Count > 0)
            return Result<RoutePlanDto>.Failure(Error.Conflict(
                "PLAN_HAS_UNASSIGNED_ORDERS", "Resolve all unassigned orders before approval."));

        var routes = await plans.GetRoutesAsync(plan.Id, ct);
        var snapshots = routes.SelectMany(route => route.Stops
                .Where(stop => stop.OrderIds is not null)
                .SelectMany(stop => stop.OrderIds!.Select(orderId =>
                    Delivery.Create(route.Id, orderId, stop.StopOrder, stop.EstimatedArrivalAt))))
            .ToList();
        await deliveries.AddRangeAsync(snapshots, ct);
        foreach (var route in routes)
            route.ReserveSuggestedVehicle();
        plan.Approve();
        if (!await plans.TrySaveChangesAsync(ct))
            return Result<RoutePlanDto>.Failure(Error.Conflict(
                "ROUTE_PLAN_APPROVAL_CONFLICT", "An order or vehicle was reserved by another approval."));

        return Result<RoutePlanDto>.Success(plan.ToDto(routes, input.Vehicles.ToDictionary(x => x.Id)));
    }
}
