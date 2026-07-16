namespace FreshFlow.Analytics.Infrastructure.CrossModule;

internal sealed class RestaurantCoordinateRow
{
    public Guid RestaurantId { get; init; }
    public string Name { get; init; } = string.Empty;
    public decimal? Latitude { get; init; }
    public decimal? Longitude { get; init; }
}
