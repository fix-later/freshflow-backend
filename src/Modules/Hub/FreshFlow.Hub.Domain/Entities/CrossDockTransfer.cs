namespace FreshFlow.Hub.Domain.Entities;

public sealed class CrossDockTransfer
{
    public const string StatusPending = "pending";
    public const string StatusInProgress = "in_progress";
    public const string StatusCompleted = "completed";

    private CrossDockTransfer() { } // EF Core

    public Guid Id { get; private set; }
    public Guid HubId { get; private set; }
    public Guid InboundEventId { get; private set; }
    public Guid OutboundRouteId { get; private set; }
    public string Status { get; private set; } = StatusPending;
    public string? Notes { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public DateTime? DeletedAt { get; private set; }

    public static CrossDockTransfer Create(
        Guid hubId,
        Guid inboundEventId,
        Guid outboundRouteId,
        string? notes)
    {
        if (hubId == Guid.Empty)
            throw new ArgumentException("Hub id is required.", nameof(hubId));

        if (inboundEventId == Guid.Empty)
            throw new ArgumentException("Inbound event id is required.", nameof(inboundEventId));

        if (outboundRouteId == Guid.Empty)
            throw new ArgumentException("Outbound route id is required.", nameof(outboundRouteId));

        var now = DateTime.UtcNow;
        return new CrossDockTransfer
        {
            Id = Guid.NewGuid(),
            HubId = hubId,
            InboundEventId = inboundEventId,
            OutboundRouteId = outboundRouteId,
            Status = StatusPending,
            Notes = NormalizeOptional(notes),
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    private static string? NormalizeOptional(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
