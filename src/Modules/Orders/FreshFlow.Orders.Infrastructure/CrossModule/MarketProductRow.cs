namespace FreshFlow.Orders.Infrastructure.CrossModule;

/// <summary>
/// Infrastructure-only EF entity — read-only projection onto market_products (Pricing module)
/// enriched with the product name from Catalog's products table. Orders reads this for
/// stock/price validation (UC-ORD-05) without a cross-module project reference.
/// </summary>
internal sealed class MarketProductRow
{
    public Guid Id { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal CurrentPrice { get; set; }
    public int CurrentQuantity { get; set; }
    public int ReservedQuantity { get; set; }
}
