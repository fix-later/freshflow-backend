namespace FreshFlow.Logistics.Infrastructure.CrossModule;

internal sealed class OrderMarketRow
{
    public Guid OrderId { get; init; }
    public string Status { get; init; } = string.Empty;
    public DateTime? ScheduledFor { get; init; }
    public Guid MarketId { get; init; }
    public string MarketName { get; init; } = string.Empty;
}
