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
        TimeOnly dailyCutoffTime,
        bool batchingEnabled,
        string defaultRouteType,
        int deliveryWindowDays,
        decimal deliveryFeePerKm,
        CancellationToken ct)
    {
        var existing = await db.Set<OperationalSettings>().FirstOrDefaultAsync(ct);
        if (existing is not null)
        {
            existing.Update(
                dailyCutoffTime, batchingEnabled, defaultRouteType, deliveryWindowDays, deliveryFeePerKm);
            await db.SaveChangesAsync(ct);
            return existing;
        }

        var created = new OperationalSettings(
            dailyCutoffTime, batchingEnabled, defaultRouteType, deliveryWindowDays, deliveryFeePerKm);
        db.Set<OperationalSettings>().Add(created);
        try
        {
            await db.SaveChangesAsync(ct);
            return created;
        }
        catch (DbUpdateException)
        {
            // Lost the first-insert race against the singleton unique index — reload the winning
            // row and apply this update onto it instead of leaving a duplicate/failed row.
            db.Entry(created).State = EntityState.Detached;
            var winner = await db.Set<OperationalSettings>().FirstAsync(ct);
            winner.Update(
                dailyCutoffTime, batchingEnabled, defaultRouteType, deliveryWindowDays, deliveryFeePerKm);
            await db.SaveChangesAsync(ct);
            return winner;
        }
    }
}
