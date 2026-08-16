namespace FreshFlow.Orders.Infrastructure.CrossModule;

internal sealed class MarketSessionGateRow
{
    public Guid MarketId { get; set; }
    public DateOnly ServiceDate { get; set; }
    public string Status { get; set; } = string.Empty;
}
