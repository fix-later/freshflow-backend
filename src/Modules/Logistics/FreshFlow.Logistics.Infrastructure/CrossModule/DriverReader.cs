using FreshFlow.Infrastructure.Persistence;
using FreshFlow.Logistics.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace FreshFlow.Logistics.Infrastructure.CrossModule;

internal sealed class DriverReader(AppDbContext db) : IDriverReader
{
    public async Task<DriverDto?> FindByUserIdAsync(Guid userId, CancellationToken ct)
    {
        var row = await db.Set<DriverRow>()
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.UserId == userId, ct);

        return row is null
            ? null
            : new DriverDto(row.UserId, row.RoleName, row.IsActive);
    }
}
