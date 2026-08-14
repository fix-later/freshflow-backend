namespace FreshFlow.Logistics.Infrastructure.CrossModule;

internal sealed class OrderStatusRow
{
    public Guid OrderId { get; init; }
    public string Status { get; init; } = string.Empty;
    public Guid RestaurantId { get; init; }
    public Guid? HubId { get; init; }
    public DateTime? ScheduledFor { get; init; }
    public decimal? DeliveryLatitude { get; init; }
    public decimal? DeliveryLongitude { get; init; }
}
