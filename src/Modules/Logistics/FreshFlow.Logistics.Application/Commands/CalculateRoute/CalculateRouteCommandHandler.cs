using FreshFlow.Logistics.Application.Abstractions;
using FreshFlow.Logistics.Application.Dtos;
using FreshFlow.Logistics.Application.Mappings;
using FreshFlow.Logistics.Domain.Entities;
using FreshFlow.Logistics.Domain.Enums;
using FreshFlow.Logistics.Domain.ValueObjects;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Logistics.Application.Commands.CalculateRoute;

internal sealed class CalculateRouteCommandHandler(
    IHubCoordinateReader hubs,
    IRestaurantCoordinateReader restaurants,
    IOrderStatusReader orders,
    IDeliveryRouteRepository routes)
    : IRequestHandler<CalculateRouteCommand, Result<RouteDto>>
{
    /// <summary>Statuses an order must be in to be worth driving to.</summary>
    private static readonly string[] RoutableOrderStatuses = ["Batched", "PickedUp", "AtHub"];

    public async Task<Result<RouteDto>> Handle(CalculateRouteCommand request, CancellationToken ct)
    {
        if (request.DestinationRestaurantIds.Count + 1 > 20)
        {
            return Result<RouteDto>.Failure(Error.Validation(
                "STOP_LIMIT_EXCEEDED",
                "A delivery route cannot contain more than 20 stops."));
        }

        var stops = new List<RouteStop>();

        var hub = await hubs.FindByIdAsync(request.HubId, ct);
        if (hub is null)
            return Result<RouteDto>.Failure(Error.NotFound("HUB", request.HubId));

        if (hub.Latitude is null || hub.Longitude is null)
        {
            return Result<RouteDto>.Failure(Error.Validation(
                "MISSING_COORDINATES",
                $"Hub '{request.HubId}' has no coordinates configured."));
        }

        stops.Add(new RouteStop(
            0,
            StopEntityType.hub,
            hub.Id,
            hub.Name,
            hub.Latitude.Value,
            hub.Longitude.Value,
            null,
            null));

        // A stop is where the *order* is going, which is the address captured at
        // checkout (`orders.delivery_latitude/longitude`) — not the restaurant's
        // default address. A restaurant that checked out to a second branch was
        // otherwise driven to its head office. `RoutePlanningInputBuilder` has
        // always read it this way; this path had not caught up.
        var routableOrders = await orders.ListByRestaurantsAndStatusAsync(
            request.DestinationRestaurantIds,
            RoutableOrderStatuses,
            ct,
            request.HubId,
            request.ServiceDate);
        var checkoutCoordinates = routableOrders
            .Where(order => order.DeliveryLatitude.HasValue && order.DeliveryLongitude.HasValue)
            .GroupBy(order => order.RestaurantId)
            .ToDictionary(
                group => group.Key,
                group => (group.First().DeliveryLatitude!.Value, group.First().DeliveryLongitude!.Value));

        foreach (var restaurantId in request.DestinationRestaurantIds)
        {
            var restaurant = await restaurants.FindByRestaurantIdAsync(restaurantId, ct);
            if (restaurant is null)
                return Result<RouteDto>.Failure(Error.NotFound("RESTAURANT", restaurantId));

            // The restaurant record supplies the stop's name; only its
            // coordinates are superseded by the order's.
            var latitude = restaurant.Latitude;
            var longitude = restaurant.Longitude;
            if (checkoutCoordinates.TryGetValue(restaurantId, out var captured))
            {
                latitude = captured.Item1;
                longitude = captured.Item2;
            }

            if (latitude is null || longitude is null)
            {
                return Result<RouteDto>.Failure(Error.Validation(
                    "MISSING_COORDINATES",
                    $"Restaurant '{restaurantId}' has no delivery coordinates for this service date."));
            }

            stops.Add(new RouteStop(
                stops.Count,
                StopEntityType.restaurant,
                restaurant.RestaurantId,
                restaurant.Name,
                latitude.Value,
                longitude.Value,
                null,
                null));
        }

        try
        {
            var route = DeliveryRoute.CreateHubRoute(
                request.HubId,
                request.ServiceDate,
                stops,
                createdBy: null);
            await routes.AddAsync(route, ct);
            await routes.SaveChangesAsync(ct);

            return Result<RouteDto>.Success(route.ToDto());
        }
        catch (ArgumentException ex)
        {
            return Result<RouteDto>.Failure(Error.Validation("VALIDATION_ERROR", ex.Message));
        }
    }
}
