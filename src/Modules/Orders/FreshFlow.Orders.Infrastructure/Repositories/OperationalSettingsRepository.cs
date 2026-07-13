using FreshFlow.Infrastructure.Persistence;
using FreshFlow.Orders.Application.Abstractions;
using FreshFlow.Orders.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FreshFlow.Orders.Infrastructure.Repositories;

internal sealed class OperationalSettingsRepository(AppDbContext db) : IOperationalSettingsRepository
{
    public async Task<OperationalSettings> GetAsync(CancellationToken ct) =>
        await db.Set<OperationalSettings>().FirstOrDefaultAsync(ct) ?? OperationalSettings.CreateDefault();

    public async Task<OperationalSettings> UpsertAsync(
        TimeOnly dailyCutoffTime, bool batchingEnabled, string defaultRouteType, CancellationToken ct)
    {
        var existing = await db.Set<OperationalSettings>().FirstOrDefaultAsync(ct);
        if (existing is null)
        {
            existing = new OperationalSettings(dailyCutoffTime, batchingEnabled, defaultRouteType);
            db.Set<OperationalSettings>().Add(existing);
        }
        else
        {
            existing.Update(dailyCutoffTime, batchingEnabled, defaultRouteType);
        }

        await db.SaveChangesAsync(ct);
        return existing;
    }
}
