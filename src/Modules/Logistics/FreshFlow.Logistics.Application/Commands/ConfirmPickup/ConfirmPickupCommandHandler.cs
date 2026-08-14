using FreshFlow.Logistics.Application.Abstractions;
using FreshFlow.Logistics.Application.Dtos;
using FreshFlow.Logistics.Domain.Entities;
using FreshFlow.Logistics.Domain.Enums;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Logistics.Application.Commands.ConfirmPickup;

internal sealed class ConfirmPickupCommandHandler(
    IDeliveryRouteRepository routes,
    IDeliveryRepository deliveries,
    IOrderStatusReader orders)
    : IRequestHandler<ConfirmPickupCommand, Result<ConfirmPickupResultDto>>
{
    private const string OrderStatusAtHub = "AtHub";

    public async Task<Result<ConfirmPickupResultDto>> Handle(ConfirmPickupCommand request, CancellationToken ct)
    {
        var route = await routes.FindByIdAsync(request.RouteId, ct);
        if (route is null)
            return Result<ConfirmPickupResultDto>.Failure(Error.NotFound("DELIVERY_ROUTE", request.RouteId));

        if (route.DriverUserId != request.DriverUserId)
        {
            return Result<ConfirmPickupResultDto>.Failure(
                Error.Unauthorized("FORBIDDEN", "This route is not assigned to the authenticated driver."));
        }

        if (route.Status != RouteStatus.assigned)
        {
            return Result<ConfirmPickupResultDto>.Failure(
                Error.Conflict("ROUTE_NOT_ASSIGNED", "Route must be assigned before dispatch."));
        }

        var restaurantStopOrders = route.Stops
            .Where(stop => stop.EntityType == StopEntityType.restaurant)
            .ToDictionary(stop => stop.EntityId, stop => stop.StopOrder);

        if (route.RoutePlanId is not null)
        {
            var snapshots = await deliveries.GetByRouteIdsAsync([route.Id], ct);
            var expectedIds = snapshots.Select(x => x.OrderId).ToHashSet();
            if (request.OrderIds.Count != expectedIds.Count || !expectedIds.SetEquals(request.OrderIds))
                return Result<ConfirmPickupResultDto>.Failure(Error.Validation(
                    "PICKUP_ORDERS_INCOMPLETE",
                    "Pickup order ids must exactly match the approved route snapshot."));
            return Result<ConfirmPickupResultDto>.Success(new ConfirmPickupResultDto(
                route.Id, snapshots.Select(x => x.Id).ToList().AsReadOnly()));
        }

        var expectedOrders = await orders.ListByRestaurantsAndStatusAsync(
            restaurantStopOrders.Keys,
            [OrderStatusAtHub],
            ct,
            route.HubId,
            route.ServiceDate);
        var expectedOrderIds = expectedOrders.Select(order => order.OrderId).ToHashSet();
        if (request.OrderIds.Count != expectedOrderIds.Count
            || !expectedOrderIds.SetEquals(request.OrderIds))
        {
            return Result<ConfirmPickupResultDto>.Failure(Error.Validation(
                "PICKUP_ORDERS_INCOMPLETE",
                "Pickup must include every AtHub order assigned to this route and hub."));
        }

        var orderStopOrders = new List<(Guid OrderId, int StopOrder)>();
        foreach (var order in expectedOrders)
        {
            if (!restaurantStopOrders.TryGetValue(order.RestaurantId, out var stopOrder))
            {
                return Result<ConfirmPickupResultDto>.Failure(
                    Error.Validation("ORDER_NOT_ON_ROUTE", "Order restaurant must be a stop on this route."));
            }

            if (await deliveries.ExistsForOrderAsync(order.OrderId, ct))
            {
                return Result<ConfirmPickupResultDto>.Failure(
                    Error.Conflict("DELIVERY_ALREADY_EXISTS", "Delivery already exists for this order."));
            }

            orderStopOrders.Add((order.OrderId, stopOrder));
        }

        var created = orderStopOrders
            .OrderBy(entry => entry.StopOrder)
            .Select((entry, index) => Delivery.Create(route.Id, entry.OrderId, index + 1))
            .ToList()
            .AsReadOnly();

        await deliveries.AddRangeAsync(created, ct);
        if (!await deliveries.TrySaveChangesAsync(ct))
        {
            return Result<ConfirmPickupResultDto>.Failure(
                Error.Conflict("DELIVERY_ALREADY_EXISTS", "Delivery already exists for this order."));
        }

        return Result<ConfirmPickupResultDto>.Success(
            new ConfirmPickupResultDto(route.Id, created.Select(d => d.Id).ToList().AsReadOnly()));
    }
}
