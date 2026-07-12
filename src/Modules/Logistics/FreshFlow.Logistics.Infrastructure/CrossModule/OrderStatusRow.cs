namespace FreshFlow.Logistics.Infrastructure.CrossModule;

internal sealed class OrderStatusRow
{
    public Guid OrderId { get; init; }
    public string Status { get; init; } = string.Empty;
    public Guid RestaurantId { get; init; }
}
