using FreshFlow.Logistics.Application.Abstractions;
using FreshFlow.Logistics.Application.Dtos;
using FreshFlow.Logistics.Domain.Enums;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Logistics.Application.Queries.GetLoadingManifest;

internal sealed class GetLoadingManifestQueryHandler(
    IDeliveryRouteRepository routes,
    IDeliveryRepository deliveries,
    IOrderStatusReader orders,
    IOrderPackingReader packing)
    : IRequestHandler<GetLoadingManifestQuery, Result<LoadingManifestDto>>
{
    private const string OrderStatusAtHub = "AtHub";

    public async Task<Result<LoadingManifestDto>> Handle(GetLoadingManifestQuery request, CancellationToken ct)
    {
        var route = await routes.FindByIdAsync(request.RouteId, ct);
        if (route is null)
            return Result<LoadingManifestDto>.Failure(Error.NotFound("DELIVERY_ROUTE", request.RouteId));

        var restaurantStops = route.Stops
            .Where(stop => stop.EntityType == StopEntityType.restaurant)
            .ToList();

        var restaurantIds = restaurantStops.Select(stop => stop.EntityId).ToList();

        var snapshots = await deliveries.GetByRouteIdsAsync([route.Id], ct);
        IReadOnlyList<OrderStatusLookupDto> atHubOrders;
        if (snapshots.Count > 0)
        {
            var snapshotIds = snapshots.Select(x => x.OrderId).ToHashSet();
            atHubOrders = restaurantStops.SelectMany(stop => (stop.OrderIds ?? [])
                .Where(snapshotIds.Contains)
                .Select(orderId => new OrderStatusLookupDto(orderId, "Snapshotted", stop.EntityId, route.HubId)))
                .ToList();
        }
        else if (route.RoutePlanId is not null)
        {
            atHubOrders = restaurantStops.SelectMany(stop => (stop.OrderIds ?? [])
                .Select(orderId => new OrderStatusLookupDto(orderId, "Proposed", stop.EntityId, route.HubId)))
                .ToList();
        }
        else
        {
            atHubOrders = await orders.ListByRestaurantsAndStatusAsync(
                restaurantIds, [OrderStatusAtHub], ct, route.HubId, route.ServiceDate);
        }

        var linesByOrder = (await packing.GetLinesByOrdersAsync(
                atHubOrders.Select(order => order.OrderId).ToList(), ct))
            .ToDictionary(entry => entry.OrderId, entry => entry.Lines);

        var stops = restaurantStops
            .Select(stop => new LoadingStopDto(
                stop.StopOrder,
                stop.EntityId,
                stop.EntityName,
                atHubOrders
                    .Where(order => order.RestaurantId == stop.EntityId)
                    .SelectMany(order => linesByOrder.GetValueOrDefault(order.OrderId, []))
                    .Select(line => new LoadingLineDto(
                        line.OrderId, line.OrderItemId, line.ProductName, line.Quantity, line.CapacityKg,
                        line.MarketProductId))
                    .ToList()))
            .Where(stop => stop.Lines.Count > 0)
            .OrderByDescending(stop => stop.StopOrder)
            .ToList();

        return Result<LoadingManifestDto>.Success(
            new LoadingManifestDto(route.Id, route.Status.ToString(), route.ServiceDate, stops));
    }
}
