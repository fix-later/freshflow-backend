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
        if (existing is null)
        {
            existing = new PricingSettings(priceAlertThresholdPercent);
            db.Set<PricingSettings>().Add(existing);
        }
        else
        {
            existing.Update(priceAlertThresholdPercent);
        }

        await db.SaveChangesAsync(ct);
        return existing;
    }
}
