using FreshFlow.Pricing.Domain.Entities;

namespace FreshFlow.Pricing.Application.Abstractions;

public interface IPricingSettingsRepository
{
    /// <summary>Returns the persisted singleton row, or <see cref="PricingSettings.CreateDefault"/> if none exists yet.</summary>
    public Task<PricingSettings> GetAsync(CancellationToken ct);

    /// <summary>Inserts the singleton row if none exists yet, otherwise updates it in place.</summary>
    public Task<PricingSettings> UpsertAsync(decimal priceAlertThresholdPercent, CancellationToken ct);
}
