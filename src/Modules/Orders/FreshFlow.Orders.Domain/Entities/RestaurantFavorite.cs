namespace FreshFlow.Orders.Domain.Entities;

/// <summary>
/// A restaurant's saved market product (server-backed wishlist, SCRUM-368). A plain on/off
/// toggle, not a soft-deletable aggregate — see RestaurantFavoriteConfiguration for why removal
/// is a hard delete.
/// </summary>
public sealed class RestaurantFavorite
{
    private RestaurantFavorite() { } // EF Core

    public RestaurantFavorite(Guid restaurantId, Guid marketProductId)
    {
        if (restaurantId == Guid.Empty)
            throw new ArgumentException("Restaurant id is required.", nameof(restaurantId));

        if (marketProductId == Guid.Empty)
            throw new ArgumentException("Market product id is required.", nameof(marketProductId));

        Id = Guid.NewGuid();
        RestaurantId = restaurantId;
        MarketProductId = marketProductId;
        CreatedAt = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid RestaurantId { get; private set; }
    public Guid MarketProductId { get; private set; }
    public DateTime CreatedAt { get; private set; }
}
