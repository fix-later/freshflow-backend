namespace FreshFlow.Hub.Domain.Entities;

public sealed class HubOutboundEvent
{
    private HubOutboundEvent() { } // EF Core

    public Guid Id { get; private set; }
    public Guid HubId { get; private set; }
    public Guid DestinationRouteId { get; private set; }
    public IReadOnlyList<HubOutboundItem> Items { get; private set; } = [];
    public decimal TotalQuantityKg { get; private set; }
    public DateTime DispatchedAt { get; private set; }
    public Guid? RecordedBy { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public DateTime? DeletedAt { get; private set; }

    public static HubOutboundEvent Record(
        Guid hubId,
        Guid destinationRouteId,
        IReadOnlyList<HubOutboundItem> items,
        DateTime dispatchedAt,
        Guid? recordedBy = null)
    {
        if (hubId == Guid.Empty)
            throw new ArgumentException("Hub id is required.", nameof(hubId));

        if (destinationRouteId == Guid.Empty)
            throw new ArgumentException("Destination route id is required.", nameof(destinationRouteId));

        if (dispatchedAt == default)
            throw new ArgumentException("Dispatched at is required.", nameof(dispatchedAt));

        var normalizedItems = ValidateAndCopyItems(items);
        var now = DateTime.UtcNow;

        return new HubOutboundEvent
        {
            Id = Guid.NewGuid(),
            HubId = hubId,
            DestinationRouteId = destinationRouteId,
            Items = normalizedItems,
            TotalQuantityKg = normalizedItems.Sum(item => item.QuantityKg),
            DispatchedAt = dispatchedAt,
            RecordedBy = recordedBy,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    private static IReadOnlyList<HubOutboundItem> ValidateAndCopyItems(IReadOnlyList<HubOutboundItem> items)
    {
        if (items.Count == 0)
            throw new ArgumentException("Outbound items are required.", nameof(items));

        foreach (var item in items)
        {
            if (item.MarketProductId == Guid.Empty)
                throw new ArgumentException("Market product id is required.", nameof(items));

            if (item.QuantityKg <= 0)
                throw new ArgumentException("Item quantity must be greater than zero.", nameof(items));
        }

        return items.ToList().AsReadOnly();
    }
}
