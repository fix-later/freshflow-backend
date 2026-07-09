namespace FreshFlow.Logistics.Infrastructure.CrossModule;

internal sealed class RestaurantCoordinateRow
{
    public Guid RestaurantId { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
}
