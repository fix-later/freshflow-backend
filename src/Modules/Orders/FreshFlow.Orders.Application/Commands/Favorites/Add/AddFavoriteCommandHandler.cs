using FreshFlow.Orders.Application.Abstractions;
using FreshFlow.Orders.Domain.Entities;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Orders.Application.Commands.Favorites.Add;

internal sealed class AddFavoriteCommandHandler(
    IFavoriteRepository favorites,
    IRestaurantReader restaurantReader,
    IMarketProductReader marketProductReader)
    : IRequestHandler<AddFavoriteCommand, Result<AddFavoriteResponse>>
{
    public async Task<Result<AddFavoriteResponse>> Handle(
        AddFavoriteCommand request, CancellationToken cancellationToken)
    {
        var restaurant = await restaurantReader.FindByUserIdAsync(request.UserId, cancellationToken);
        if (restaurant is null)
            return Result<AddFavoriteResponse>.Failure(
                Error.Unauthorized("FORBIDDEN", "The authenticated user has no associated restaurant."));

        var marketProduct = await marketProductReader.FindAsync(request.MarketProductId, cancellationToken);
        if (marketProduct is null)
            return Result<AddFavoriteResponse>.Failure(
                Error.NotFound("MARKET_PRODUCT", request.MarketProductId));

        // AddAsync no-ops on a unique-index conflict — favoriting an already-favorited
        // product is a success, not an error.
        await favorites.AddAsync(
            new RestaurantFavorite(restaurant.RestaurantId, request.MarketProductId),
            cancellationToken);

        return Result<AddFavoriteResponse>.Success(new AddFavoriteResponse(request.MarketProductId));
    }
}
