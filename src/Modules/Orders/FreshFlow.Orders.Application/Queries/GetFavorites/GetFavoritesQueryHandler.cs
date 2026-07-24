using FreshFlow.Orders.Application.Abstractions;
using FreshFlow.Orders.Application.Dtos;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Orders.Application.Queries.GetFavorites;

internal sealed class GetFavoritesQueryHandler(
    IFavoriteReader favorites,
    IRestaurantReader restaurantReader)
    : IRequestHandler<GetFavoritesQuery, Result<IReadOnlyList<FavoriteItemDto>>>
{
    public async Task<Result<IReadOnlyList<FavoriteItemDto>>> Handle(
        GetFavoritesQuery request, CancellationToken cancellationToken)
    {
        var restaurant = await restaurantReader.FindByUserIdAsync(request.UserId, cancellationToken);
        if (restaurant is null)
            return Result<IReadOnlyList<FavoriteItemDto>>.Failure(
                Error.Unauthorized("FORBIDDEN", "The authenticated user has no associated restaurant."));

        var items = await favorites.ListAsync(restaurant.RestaurantId, cancellationToken);

        return Result<IReadOnlyList<FavoriteItemDto>>.Success(items);
    }
}
