namespace FreshFlow.Pricing.Domain.Entities;

/// <summary>
/// Append-only, immutable record of a price/quantity point-in-time snapshot.
/// Does NOT extend BaseEntity — no soft delete, no UpdatedAt.
/// Once created, nothing changes.
/// </summary>
public sealed class PriceSnapshot
{
    private PriceSnapshot() { } // EF Core

    public PriceSnapshot(Guid marketProductId, decimal price, int quantity, Guid? recordedBy)
    {
        MarketProductId = marketProductId;
        Price = price;
        Quantity = quantity;
        RecordedBy = recordedBy;
        RecordedAt = DateTime.UtcNow;
    }

    public Guid Id { get; private init; } = Guid.NewGuid();
    public Guid MarketProductId { get; private init; }
    public decimal Price { get; private init; }
    public int Quantity { get; private init; }
    public Guid? RecordedBy { get; private init; }
    public DateTime RecordedAt { get; private init; }

    /// <summary>
    /// Single source of truth for snapshot construction after a price or quantity update.
    /// Captures the post-change state of <paramref name="marketProduct"/>.
    /// </summary>
    public static PriceSnapshot For(MarketProduct marketProduct, Guid? actor) =>
        new(marketProduct.Id, marketProduct.CurrentPrice, marketProduct.CurrentQuantity, actor);
}
