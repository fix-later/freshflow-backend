namespace FreshFlow.Pricing.Application.Dtos;

public sealed record PricingSettingsDto(decimal PriceAlertThresholdPercent, DateTime UpdatedAt);
