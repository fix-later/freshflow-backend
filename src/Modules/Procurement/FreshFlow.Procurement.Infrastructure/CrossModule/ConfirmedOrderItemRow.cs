namespace FreshFlow.Procurement.Infrastructure.CrossModule;

internal sealed class ConfirmedOrderItemRow
{
    public Guid OrderId { get; set; }
    public Guid MarketProductId { get; set; }
    public string ProductNameSnapshot { get; set; } = string.Empty;
    public int Quantity { get; set; }
}
