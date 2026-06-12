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

    /// <summary>
    /// Applies a price-only update. Raises <see cref="PriceUpdatedDomainEvent"/>.
    /// Caller must ensure <paramref name="newPrice"/> is positive (domain defends in depth).
    /// </summary>
    public void UpdatePrice(decimal newPrice, Guid? actor)
    {
        ValidatePrice(newPrice);

        var oldPrice = CurrentPrice;
        CurrentPrice = newPrice;
        UpdatedBy = actor;
        UpdatedAt = DateTime.UtcNow;

        RaiseDomainEvent(new PriceUpdatedDomainEvent(
            Id, MarketId, ProductId, oldPrice, CurrentPrice, CurrentQuantity, actor, DateTime.UtcNow));
    }

    /// <summary>
    /// Applies a quantity-only mutation. Does NOT raise a domain event
    /// (use <see cref="ApplyUpdate"/> when price/quantity update must be recorded together).
    /// </summary>
    public void UpdateQuantity(int newQuantity, Guid? actor)
    {
        ValidateQuantity(newQuantity);

        CurrentQuantity = newQuantity;
        UpdatedBy = actor;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Combined update of price and/or quantity in a single operation.
    /// Raises exactly one <see cref="PriceUpdatedDomainEvent"/> carrying the full context
    /// needed by downstream handlers (SignalR broadcast, Redis sync, price history).
    ///
    /// The handler (UC-PRI-03) pre-validates business rules before calling this method.
    /// Domain-level guards are retained as a defence-in-depth layer.
    /// </summary>
    public void ApplyUpdate(decimal? newPrice, int? newQuantity, Guid? actor)
    {
        var previousPrice = CurrentPrice;

        if (newPrice.HasValue)
        {
            ValidatePrice(newPrice.Value);
            CurrentPrice = newPrice.Value;
        }

        if (newQuantity.HasValue)
        {
            ValidateQuantity(newQuantity.Value);
            CurrentQuantity = newQuantity.Value;
        }

        UpdatedBy = actor;
        UpdatedAt = DateTime.UtcNow;

        RaiseDomainEvent(new PriceUpdatedDomainEvent(
            Id, MarketId, ProductId, previousPrice, CurrentPrice, CurrentQuantity, actor, DateTime.UtcNow));
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
