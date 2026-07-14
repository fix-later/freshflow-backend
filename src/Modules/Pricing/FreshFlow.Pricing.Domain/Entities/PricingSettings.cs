using FreshFlow.SharedKernel.Domain;

namespace FreshFlow.Pricing.Domain.Entities;

/// <summary>
/// Singleton admin-editable pricing config (SCRUM-357). At most one row exists; repository
/// callers fall back to <see cref="CreateDefault"/> when no row has been persisted yet.
/// </summary>
public sealed class PricingSettings : BaseEntity
{
    private PricingSettings() { } // EF Core

    public PricingSettings(decimal priceAlertThresholdPercent)
    {
        PriceAlertThresholdPercent = priceAlertThresholdPercent;
    }

    public static PricingSettings CreateDefault() => new(10.00m);

    public decimal PriceAlertThresholdPercent { get; private set; }

    public void Update(decimal priceAlertThresholdPercent)
    {
        PriceAlertThresholdPercent = priceAlertThresholdPercent;
        UpdatedAt = DateTime.UtcNow;
    }
}
