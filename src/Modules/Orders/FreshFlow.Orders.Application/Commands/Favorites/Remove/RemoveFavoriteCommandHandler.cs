using FreshFlow.Orders.Application.Abstractions;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Orders.Application.Commands.Favorites.Remove;

internal sealed class RemoveFavoriteCommandHandler(
    IFavoriteRepository favorites,
    IRestaurantReader restaurantReader)
    : IRequestHandler<RemoveFavoriteCommand, Result>
{
    public async Task<Result> Handle(RemoveFavoriteCommand request, CancellationToken cancellationToken)
    {
        var restaurant = await restaurantReader.FindByUserIdAsync(request.UserId, cancellationToken);
        if (restaurant is null)
            return Result.Failure(
                Error.Unauthorized("FORBIDDEN", "The authenticated user has no associated restaurant."));

        // Not-favorited is also a success — un-favoriting twice is not an error.
        await favorites.RemoveAsync(restaurant.RestaurantId, request.MarketProductId, cancellationToken);

        return Result.Success();
    }
}
