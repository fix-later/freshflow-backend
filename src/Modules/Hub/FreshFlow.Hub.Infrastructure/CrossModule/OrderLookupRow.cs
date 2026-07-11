namespace FreshFlow.Hub.Infrastructure.CrossModule;

internal sealed class OrderLookupRow
{
    public Guid OrderItemId { get; init; }
    public Guid OrderId { get; init; }
    public Guid MarketProductId { get; init; }
    public decimal Quantity { get; init; }
    public decimal? ActualQuantity { get; init; }
}
