namespace FreshFlow.Procurement.Infrastructure.CrossModule;

internal sealed class MarketProductMarketRow
{
    public Guid Id { get; set; }
    public Guid MarketId { get; set; }
    public decimal CurrentPrice { get; set; }
    public DateTime? DeletedAt { get; set; }
}
