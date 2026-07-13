using FreshFlow.Infrastructure.Persistence;
using FreshFlow.Logistics.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace FreshFlow.Logistics.Infrastructure.CrossModule;

internal sealed class RestaurantOwnerReader(AppDbContext db) : IRestaurantOwnerReader
{
    public async Task<Guid?> FindRestaurantIdByUserIdAsync(Guid userId, CancellationToken ct)
    {
        if (userId == Guid.Empty)
            return null;

        return await db.Set<RestaurantOwnerRow>()
            .AsNoTracking()
            .Where(r => r.UserId == userId)
            .Select(r => (Guid?)r.RestaurantId)
            .FirstOrDefaultAsync(ct);
    }
}
