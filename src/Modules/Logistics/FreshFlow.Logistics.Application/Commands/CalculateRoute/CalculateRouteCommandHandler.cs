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
    IMarketCoordinateReader markets,
    IRestaurantCoordinateReader restaurants,
    IDeliveryRouteRepository routes)
    : IRequestHandler<CalculateRouteCommand, Result<RouteDto>>
{
    public async Task<Result<RouteDto>> Handle(CalculateRouteCommand request, CancellationToken ct)
    {
        if (request.HubIds.Count > 0 || request.CompareWithHub)
        {
            return Result<RouteDto>.Failure(Error.Validation(
                "HUB_RELAY_NOT_SUPPORTED",
                "HUB_RELAY routing is not yet supported -- pending the Hub module (SCRUM-256)."));
        }

        if (request.SourceMarketIds.Count + request.DestinationRestaurantIds.Count > 20)
        {
            return Result<RouteDto>.Failure(Error.Validation(
                "STOP_LIMIT_EXCEEDED",
                "A delivery route cannot contain more than 20 stops."));
        }

        // OptimizationCriteria is validated here but only consumed by SCRUM-305's
        // optimization engine, which is not implemented yet.
        var stops = new List<RouteStop>();

        foreach (var marketId in request.SourceMarketIds)
        {
            var market = await markets.FindByIdAsync(marketId, ct);
            if (market is null)
                return Result<RouteDto>.Failure(Error.NotFound("MARKET", marketId));

            if (market.Latitude is null || market.Longitude is null)
            {
                return Result<RouteDto>.Failure(Error.Validation(
                    "MISSING_COORDINATES",
                    $"Market '{marketId}' has no coordinates configured."));
            }

            stops.Add(new RouteStop(
                stops.Count,
                StopEntityType.market,
                market.Id,
                market.Name,
                market.Latitude.Value,
                market.Longitude.Value,
                null,
                null));
        }

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
            var route = DeliveryRoute.CreateDirect(request.ServiceDate, stops, createdBy: null);
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
