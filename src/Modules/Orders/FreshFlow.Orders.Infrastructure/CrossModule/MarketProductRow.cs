namespace FreshFlow.Orders.Infrastructure.CrossModule;

/// <summary>
/// Infrastructure-only EF entity — read-only projection onto market_products (Pricing module)
/// enriched with the product name from Catalog's products table. Orders reads this for
/// stock/price validation (UC-ORD-05) without a cross-module project reference.
/// </summary>
internal sealed class MarketProductRow
{
    public Guid Id { get; set; }
    public Guid MarketId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal CurrentPrice { get; set; }
    public int CurrentQuantity { get; set; }
    public int ReservedQuantity { get; set; }
    public int MinimumOrderQuantity { get; set; }
    public string? VatRate { get; set; }
    public decimal? OriginLatitude { get; set; }
    public decimal? OriginLongitude { get; set; }
    public string? PackingCode { get; set; }

    /// <summary>`packing_codes.CapacityKg` — what one box of this product holds.</summary>
    public decimal? PackingWeightKg { get; set; }
}
