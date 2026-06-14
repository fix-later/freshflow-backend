namespace FreshFlow.Pricing.Application.Options;

/// <summary>
/// Configuration options for the Pricing module.
/// Bound from the "Pricing" section in appsettings.
/// </summary>
public sealed class PricingOptions
{
    public const string SectionName = "Pricing";

    /// <summary>
    /// Maximum allowed product price in VND.
    /// Prices above this value are rejected by the validator.
    /// Default: 50,000,000 VND.
    /// </summary>
    public decimal MaxPriceVnd { get; set; } = 50_000_000m;
}
