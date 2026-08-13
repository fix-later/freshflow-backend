namespace FreshFlow.Procurement.Infrastructure.CrossModule;

internal sealed class MarketSessionTrackingOrderRow
{
    public Guid MarketSessionId { get; init; }
    public Guid OrderId { get; init; }
    public Guid RestaurantId { get; init; }
    public string RestaurantName { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public decimal TotalAmount { get; init; }
    public DateTime? ConfirmedAt { get; init; }
    public Guid? OrderItemId { get; init; }
    public Guid? MarketProductId { get; init; }
    public string? ProductName { get; init; }
    public int? Quantity { get; init; }
    public decimal? UnitPrice { get; init; }
    public decimal? Subtotal { get; init; }
}
