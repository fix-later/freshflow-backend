namespace FreshFlow.Logistics.Infrastructure.CrossModule;

internal sealed class MarketCoordinateRow
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
}
