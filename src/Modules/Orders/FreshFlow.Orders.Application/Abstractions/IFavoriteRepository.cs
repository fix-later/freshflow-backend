using FreshFlow.Orders.Domain.Entities;

namespace FreshFlow.Orders.Application.Abstractions;

public interface IFavoriteRepository
{
    /// <summary>
    /// Inserts the favorite. Returns false (no-op) if the pair already existed — the unique
    /// index on (restaurant_id, market_product_id) is what makes this idempotent.
    /// </summary>
    public Task<bool> AddAsync(RestaurantFavorite favorite, CancellationToken ct);

    /// <summary>Removes the favorite. Returns false if it did not exist — also idempotent.</summary>
    public Task<bool> RemoveAsync(Guid restaurantId, Guid marketProductId, CancellationToken ct);
}
