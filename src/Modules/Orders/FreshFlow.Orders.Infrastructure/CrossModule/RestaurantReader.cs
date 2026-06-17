using FreshFlow.Infrastructure.Persistence;
using FreshFlow.Orders.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace FreshFlow.Orders.Infrastructure.CrossModule;

internal sealed class RestaurantReader(AppDbContext db) : IRestaurantReader
{
    private const string ActiveStatus = "active";

    public async Task<RestaurantSnapshotDto?> FindByUserIdAsync(Guid userId, CancellationToken ct)
    {
        var row = await db.Set<RestaurantRow>()
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.UserId == userId, ct);

        return row is null
            ? null
            : new RestaurantSnapshotDto(
                row.Id,
                string.Equals(row.Status, ActiveStatus, StringComparison.OrdinalIgnoreCase));
    }
}
