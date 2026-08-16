namespace FreshFlow.Logistics.Infrastructure.CrossModule;

internal sealed class HubCoordinateRow
{
    public Guid Id { get; init; }
    public Guid? MarketId { get; init; }
    public string Name { get; init; } = string.Empty;
    public decimal? Latitude { get; init; }
    public decimal? Longitude { get; init; }
}
