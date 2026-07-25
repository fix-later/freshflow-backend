namespace FreshFlow.Orders.Infrastructure.CrossModule;

/// <summary>
/// Infrastructure-only EF entity — read-only projection joining restaurant_favorites (Orders'
/// own table) with market_products (Pricing), products/markets/product_categories (Catalog),
/// and units_of_measurement (Catalog). A new seam rather than extending MarketProductRow:
/// MarketProductRow is a lean projection for the order-validation hot path, while favorites
/// needs a different shape (it JOINs restaurant_favorites itself, plus market/unit/category
/// names for the FE card) — bolting that onto the hot-path row would be scope creep there.
/// </summary>
internal sealed class FavoriteListItemRow
{
    public Guid RestaurantId { get; set; }
    public Guid MarketProductId { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public Guid MarketId { get; set; }
    public string MarketName { get; set; } = string.Empty;
    public string? Category { get; set; }
    public string? Unit { get; set; }
    public decimal CurrentPrice { get; set; }
    public int AvailableQuantity { get; set; }
}
