namespace FreshFlow.Analytics.Infrastructure.CrossModule;

internal sealed class HubOutboundEventRow
{
    public Guid OutboundEventId { get; init; }
    public Guid HubId { get; init; }
    public Guid DestinationRouteId { get; init; }
    public decimal TotalQuantityKg { get; init; }
    public DateTime DispatchedAt { get; init; }
}

