namespace FreshFlow.Procurement.Infrastructure.CrossModule;

internal sealed class MarketProductImageRow
{
    public Guid MarketProductId { get; init; }
    public string? ImageUrl { get; init; }
    public string? PackingCode { get; init; }
    public decimal? PackingCapacityKg { get; init; }
}
