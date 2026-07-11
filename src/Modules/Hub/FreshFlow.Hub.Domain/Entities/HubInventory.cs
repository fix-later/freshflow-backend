namespace FreshFlow.Hub.Domain.Entities;

public sealed class HubInventory
{
    private HubInventory() { } // EF Core

    public Guid Id { get; private set; }
    public Guid HubId { get; private set; }
    public Guid MarketProductId { get; private set; }
    public decimal QuantityIn { get; private set; }
    public decimal QuantityOut { get; private set; }
    public decimal QuantityAvailable { get; private set; }
    public DateTime RecordedAt { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public DateTime? DeletedAt { get; private set; }

    public static HubInventory Create(Guid hubId, Guid marketProductId)
    {
        if (hubId == Guid.Empty)
            throw new ArgumentException("Hub id is required.", nameof(hubId));

        if (marketProductId == Guid.Empty)
            throw new ArgumentException("Market product id is required.", nameof(marketProductId));

        var now = DateTime.UtcNow;
        return new HubInventory
        {
            Id = Guid.NewGuid(),
            HubId = hubId,
            MarketProductId = marketProductId,
            QuantityIn = 0m,
            QuantityOut = 0m,
            RecordedAt = now,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public void AddInbound(decimal quantity)
    {
        if (quantity <= 0)
            throw new ArgumentException("Inbound quantity must be greater than zero.", nameof(quantity));

        QuantityIn += quantity;
        UpdatedAt = DateTime.UtcNow;
    }

    public void AddOutbound(decimal quantity)
    {
        if (quantity <= 0)
            throw new ArgumentException("Outbound quantity must be greater than zero.", nameof(quantity));

        QuantityOut += quantity;
        UpdatedAt = DateTime.UtcNow;
    }
}
