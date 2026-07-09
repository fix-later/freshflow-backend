using FreshFlow.Infrastructure.Persistence;
using FreshFlow.Logistics.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace FreshFlow.Logistics.Infrastructure.CrossModule;

internal sealed class RestaurantCoordinateReader(AppDbContext db) : IRestaurantCoordinateReader
{
    public async Task<RestaurantCoordinateDto?> FindByRestaurantIdAsync(
        Guid restaurantId,
        CancellationToken ct)
    {
        var row = await db.Set<RestaurantCoordinateRow>()
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.RestaurantId == restaurantId, ct);

        return row is null
            ? null
            : new RestaurantCoordinateDto(row.RestaurantId, row.Latitude, row.Longitude);
    }
}
