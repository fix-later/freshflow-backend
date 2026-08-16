using FreshFlow.Logistics.Application.Abstractions;
using FreshFlow.Logistics.Application.Dtos;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Logistics.Application.Queries.GetRouteSuggestions;

internal sealed class GetRouteSuggestionsQueryHandler(
    IOrderStatusReader orders,
    IOrderMarketReader orderMarkets,
    IHubCoordinateReader hubs,
    IRestaurantCoordinateReader restaurants)
    : IRequestHandler<GetRouteSuggestionsQuery, Result<RouteSuggestionsDto>>
{
    public async Task<Result<RouteSuggestionsDto>> Handle(
        GetRouteSuggestionsQuery request,
        CancellationToken ct)
    {
        IReadOnlyCollection<string> statuses = request.IncludeBatched
            ? ["AtHub", "Batched"]
            : ["AtHub"];

        var marketCounts = await orderMarkets.ListRoutableMarketsAsync(
            request.ServiceDate, statuses, ct);
        var restaurantCounts = await orders.ListRoutableRestaurantsAsync(
            request.ServiceDate, statuses, ct);

        var hubSuggestions = new List<SuggestionItemDto>(marketCounts.Count);
        foreach (var market in marketCounts)
        {
            var hub = await hubs.FindByMarketIdAsync(market.MarketId, ct);
            if (hub is not null)
                hubSuggestions.Add(new SuggestionItemDto(hub.Id, hub.Name, market.OrderCount));
        }

        var restaurantSuggestions = new List<SuggestionItemDto>(restaurantCounts.Count);
        foreach (var (restaurantId, orderCount) in restaurantCounts)
        {
            var restaurant = await restaurants.FindByRestaurantIdAsync(restaurantId, ct);
            if (restaurant is not null)
                restaurantSuggestions.Add(new SuggestionItemDto(restaurantId, restaurant.Name, orderCount));
        }

        return Result<RouteSuggestionsDto>.Success(new RouteSuggestionsDto(
            request.ServiceDate,
            hubSuggestions,
            restaurantSuggestions));
    }
}
