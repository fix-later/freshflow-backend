using FreshFlow.Infrastructure.Persistence;
using FreshFlow.Pricing.Application.Abstractions;
using FreshFlow.Pricing.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FreshFlow.Pricing.Infrastructure.Repositories;

internal sealed class PricingSettingsRepository(AppDbContext db) : IPricingSettingsRepository
{
    public async Task<PricingSettings> GetAsync(CancellationToken ct) =>
        await db.Set<PricingSettings>().FirstOrDefaultAsync(ct) ?? PricingSettings.CreateDefault();

    public async Task<PricingSettings> UpsertAsync(decimal priceAlertThresholdPercent, CancellationToken ct)
    {
        var existing = await db.Set<PricingSettings>().FirstOrDefaultAsync(ct);
        if (existing is not null)
        {
            existing.Update(priceAlertThresholdPercent);
            await db.SaveChangesAsync(ct);
            return existing;
        }

        var created = new PricingSettings(priceAlertThresholdPercent);
        db.Set<PricingSettings>().Add(created);
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
            var winner = await db.Set<PricingSettings>().FirstAsync(ct);
            winner.Update(priceAlertThresholdPercent);
            await db.SaveChangesAsync(ct);
            return winner;
        }
    }
}
