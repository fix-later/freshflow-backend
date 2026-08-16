namespace FreshFlow.Hub.Domain.Entities;

public sealed class HubInboundEvent
{
    public const string StatusPending = "PENDING";
    public const string StatusArrivedAtHub = "ARRIVED_AT_HUB";
    public const string ConditionOk = "OK";

    private HubInboundEvent() { } // EF Core

    public Guid Id { get; private set; }
    public Guid HubId { get; private set; }
    public Guid? SourceMarketId { get; private set; }
    public Guid? DeliveryRouteId { get; private set; }
    public Guid? DeliveryScheduleId { get; private set; }
    public IReadOnlyList<HubInboundItem> Items { get; private set; } = [];
    public decimal TotalQuantityKg { get; private set; }
    public DateTime ArrivedAt { get; private set; }
    public Guid? RecordedBy { get; private set; }
    public Guid? HubStaffUserId { get; private set; }
    public string Status { get; private set; } = StatusPending;
    public string ConditionStatus { get; private set; } = ConditionOk;
    public string? DiscrepancyNotes { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public DateTime? DeletedAt { get; private set; }

    public static HubInboundEvent Record(
        Guid hubId,
        Guid? sourceMarketId,
        Guid? deliveryRouteId,
        Guid? deliveryScheduleId,
        IReadOnlyList<HubInboundItem> items,
        DateTime arrivedAt,
        Guid? recordedBy = null,
        Guid? hubStaffUserId = null)
    {
        if (hubId == Guid.Empty)
            throw new ArgumentException("Hub id is required.", nameof(hubId));

        if (arrivedAt == default)
            throw new ArgumentException("Arrived at is required.", nameof(arrivedAt));

        var normalizedItems = ValidateAndCopyItems(items);
        var now = DateTime.UtcNow;

        return new HubInboundEvent
        {
            Id = Guid.NewGuid(),
            HubId = hubId,
            SourceMarketId = sourceMarketId,
            DeliveryRouteId = deliveryRouteId,
            DeliveryScheduleId = deliveryScheduleId,
            Items = normalizedItems,
            TotalQuantityKg = normalizedItems.Sum(item => item.QuantityKg),
            ArrivedAt = arrivedAt,
            RecordedBy = recordedBy,
            HubStaffUserId = hubStaffUserId,
            Status = StatusPending,
            ConditionStatus = ConditionOk,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public void ConfirmArrival(Guid? hubStaffUserId = null)
    {
        if (Status != StatusPending)
            throw new InvalidOperationException("Only pending inbound events can be confirmed.");

        Status = StatusArrivedAtHub;
        if (hubStaffUserId.HasValue)
            HubStaffUserId = hubStaffUserId;
        UpdatedAt = DateTime.UtcNow;
    }

    private static IReadOnlyList<HubInboundItem> ValidateAndCopyItems(IReadOnlyList<HubInboundItem> items)
    {
        if (items.Count == 0)
            throw new ArgumentException("Inbound items are required.", nameof(items));

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
