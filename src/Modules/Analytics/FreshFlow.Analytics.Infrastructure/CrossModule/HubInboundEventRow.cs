namespace FreshFlow.Analytics.Infrastructure.CrossModule;

internal sealed class HubInboundEventRow
{
    public Guid InboundEventId { get; init; }
    public string Status { get; init; } = string.Empty;
    public decimal TotalQuantityKg { get; init; }
    public DateTime ArrivedAt { get; init; }
}

