using FreshFlow.Pricing.Domain.Events;
using FreshFlow.SharedKernel.Domain;

namespace FreshFlow.Pricing.Domain.Entities;

public sealed class MarketProduct : AggregateRoot
{
    private MarketProduct() { } // EF Core

    public MarketProduct(Guid marketId, Guid productId, decimal initialPrice, int initialQuantity, Guid? createdBy)
    {
        ValidatePrice(initialPrice);
        ValidateQuantity(initialQuantity);

        MarketId = marketId;
        ProductId = productId;
        CurrentPrice = initialPrice;
        CurrentQuantity = initialQuantity;
        ReservedQuantity = 0;
        UpdatedBy = createdBy;
    }

    public Guid MarketId { get; private set; }
    public Guid ProductId { get; private set; }
    public decimal CurrentPrice { get; private set; }
    public int CurrentQuantity { get; private set; }
    public int ReservedQuantity { get; private set; }
    public Guid? UpdatedBy { get; private set; }

    public int AvailableQuantity => CurrentQuantity - ReservedQuantity;
    public bool IsOutOfStock => CurrentQuantity == 0;

    public void UpdatePrice(decimal newPrice, Guid? actor)
    {
        ValidatePrice(newPrice);

        var oldPrice = CurrentPrice;
        CurrentPrice = newPrice;
        UpdatedBy = actor;
        UpdatedAt = DateTime.UtcNow;

        RaiseDomainEvent(new PriceUpdatedDomainEvent(Id, oldPrice, newPrice, actor, DateTime.UtcNow));
    }

    public void UpdateQuantity(int newQuantity, Guid? actor)
    {
        ValidateQuantity(newQuantity);

        CurrentQuantity = newQuantity;
        UpdatedBy = actor;
        UpdatedAt = DateTime.UtcNow;
    }

    // ── Invariant helpers ────────────────────────────────────────────────────

    private static void ValidatePrice(decimal price)
    {
        if (price < 0)
            throw new ArgumentOutOfRangeException(nameof(price), price, "Price must be non-negative.");
    }

    private static void ValidateQuantity(int quantity)
    {
        if (quantity < 0)
            throw new ArgumentOutOfRangeException(nameof(quantity), quantity, "Quantity must be non-negative.");
    }
}
