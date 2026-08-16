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
            : new DriverDto(row.UserId, row.FullName, row.Email, row.RoleName, row.IsActive);
    }

    public Task<bool> IsAssignedToHubAsync(Guid userId, Guid hubId, CancellationToken ct) =>
        db.Set<DriverRow>()
            .AsNoTracking()
            .AnyAsync(d => d.UserId == userId && d.HubId == hubId, ct);

    public async Task<IReadOnlyList<DriverDto>> ListEligibleAsync(Guid hubId, CancellationToken ct) =>
        await db.Set<DriverRow>()
            .AsNoTracking()
            .Where(d => d.HubId == hubId && d.RoleName == "driver" && d.IsActive)
            .Select(d => new DriverDto(d.UserId, d.FullName, d.Email, d.RoleName, d.IsActive))
            .ToListAsync(ct);
}
