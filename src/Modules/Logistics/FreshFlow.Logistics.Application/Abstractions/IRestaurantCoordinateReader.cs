namespace FreshFlow.Logistics.Application.Abstractions;

public interface IRestaurantCoordinateReader
{
    public Task<RestaurantCoordinateDto?> FindByRestaurantIdAsync(
        Guid restaurantId,
        CancellationToken ct);
}

public sealed record RestaurantCoordinateDto(Guid RestaurantId, decimal? Latitude, decimal? Longitude);
