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

        var routeRestaurantIds = route.Stops
            .Where(stop => stop.EntityType == StopEntityType.restaurant)
            .Select(stop => stop.EntityId)
            .ToHashSet();

        foreach (var orderId in request.OrderIds)
        {
            var order = await orders.FindByIdAsync(orderId, ct);
            if (order is null)
                return Result<ConfirmPickupResultDto>.Failure(Error.NotFound("ORDER", orderId));

            if (order.Status != OrderStatusAtHub)
            {
                return Result<ConfirmPickupResultDto>.Failure(
                    Error.Validation("ORDER_NOT_AT_HUB", "Order must be at hub before dispatch."));
            }

            if (!routeRestaurantIds.Contains(order.RestaurantId))
            {
                return Result<ConfirmPickupResultDto>.Failure(
                    Error.Validation("ORDER_NOT_ON_ROUTE", "Order restaurant must be a stop on this route."));
            }

            if (await deliveries.ExistsForOrderAsync(orderId, ct))
            {
                return Result<ConfirmPickupResultDto>.Failure(
                    Error.Conflict("DELIVERY_ALREADY_EXISTS", "Delivery already exists for this order."));
            }
        }

        var created = request.OrderIds
            .Select((orderId, index) => Delivery.Create(route.Id, orderId, index + 1))
            .ToList()
            .AsReadOnly();

        await deliveries.AddRangeAsync(created, ct);
        await deliveries.SaveChangesAsync(ct);

        return Result<ConfirmPickupResultDto>.Success(
            new ConfirmPickupResultDto(route.Id, created.Select(d => d.Id).ToList().AsReadOnly()));
    }
}
