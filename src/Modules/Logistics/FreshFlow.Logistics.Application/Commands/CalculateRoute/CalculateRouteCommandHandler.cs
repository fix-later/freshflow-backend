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
    IDeliveryRouteRepository routes)
    : IRequestHandler<CalculateRouteCommand, Result<RouteDto>>
{
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

        foreach (var restaurantId in request.DestinationRestaurantIds)
        {
            var restaurant = await restaurants.FindByRestaurantIdAsync(restaurantId, ct);
            if (restaurant is null)
                return Result<RouteDto>.Failure(Error.NotFound("RESTAURANT", restaurantId));

            if (restaurant.Latitude is null || restaurant.Longitude is null)
            {
                return Result<RouteDto>.Failure(Error.Validation(
                    "MISSING_COORDINATES",
                    $"Restaurant '{restaurantId}' has no coordinates configured."));
            }

            stops.Add(new RouteStop(
                stops.Count,
                StopEntityType.restaurant,
                restaurant.RestaurantId,
                restaurant.Name,
                restaurant.Latitude.Value,
                restaurant.Longitude.Value,
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
