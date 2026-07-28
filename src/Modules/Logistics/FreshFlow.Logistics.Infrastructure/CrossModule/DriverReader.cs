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

    public async Task<IReadOnlyList<DriverDto>> ListEligibleAsync(CancellationToken ct) =>
        await db.Set<DriverRow>()
            .AsNoTracking()
            .Where(d => d.RoleName == "driver" && d.IsActive)
            .Select(d => new DriverDto(d.UserId, d.RoleName, d.IsActive))
            .ToListAsync(ct);
}
