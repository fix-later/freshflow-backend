namespace FreshFlow.Logistics.Infrastructure.CrossModule;

internal sealed class OrderPackingLineRow
{
    public Guid OrderId { get; init; }
    public Guid OrderItemId { get; init; }
    public string ProductName { get; init; } = string.Empty;
    public int Quantity { get; init; }
    public decimal? CapacityKg { get; init; }
}
