namespace FreshFlow.Hub.Domain.Entities;

public sealed class HubHandoverEvent
{
    public const string StatusPendingCheckout = "PENDING_CHECKOUT";
    public const string StatusCheckedOut = "CHECKED_OUT";

    private HubHandoverEvent() { } // EF Core

    public Guid Id { get; private set; }
    public Guid HubId { get; private set; }
    public Guid DeliveryRouteId { get; private set; }
    public Guid DriverUserId { get; private set; }
    public Guid? OutboundEventId { get; private set; }
    public string Status { get; private set; } = StatusPendingCheckout;
    public Guid HandedOverBy { get; private set; }
    public DateTime HandedOverAt { get; private set; }
    public DateTime? DriverConfirmedAt { get; private set; }
    public string? Notes { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public DateTime? DeletedAt { get; private set; }

    public static HubHandoverEvent Create(
        Guid hubId,
        Guid deliveryRouteId,
        Guid driverUserId,
        Guid? outboundEventId,
        Guid handedOverBy,
        string? notes)
    {
        if (hubId == Guid.Empty)
            throw new ArgumentException("Hub id is required.", nameof(hubId));

        if (deliveryRouteId == Guid.Empty)
            throw new ArgumentException("Delivery route id is required.", nameof(deliveryRouteId));

        if (driverUserId == Guid.Empty)
            throw new ArgumentException("Driver user id is required.", nameof(driverUserId));

        if (outboundEventId == Guid.Empty)
            throw new ArgumentException("Outbound event id cannot be empty.", nameof(outboundEventId));

        if (handedOverBy == Guid.Empty)
            throw new ArgumentException("Handed over by is required.", nameof(handedOverBy));

        var now = DateTime.UtcNow;
        return new HubHandoverEvent
        {
            Id = Guid.NewGuid(),
            HubId = hubId,
            DeliveryRouteId = deliveryRouteId,
            DriverUserId = driverUserId,
            OutboundEventId = outboundEventId,
            Status = StatusPendingCheckout,
            HandedOverBy = handedOverBy,
            HandedOverAt = now,
            Notes = NormalizeOptional(notes),
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public void ConfirmCheckout(Guid driverUserId)
    {
        if (Status != StatusPendingCheckout)
            throw new InvalidOperationException("Hub handover has already been checked out.");

        if (driverUserId != DriverUserId)
            throw new ArgumentException("Only the assigned driver can confirm checkout.", nameof(driverUserId));

        var now = DateTime.UtcNow;
        Status = StatusCheckedOut;
        DriverConfirmedAt = now;
        UpdatedAt = now;
    }

    private static string? NormalizeOptional(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
