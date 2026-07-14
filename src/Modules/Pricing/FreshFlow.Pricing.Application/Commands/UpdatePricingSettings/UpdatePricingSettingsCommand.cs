using FreshFlow.Pricing.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Pricing.Application.Commands.UpdatePricingSettings;

public sealed record UpdatePricingSettingsCommand(decimal PriceAlertThresholdPercent) : ICommand<PricingSettingsDto>;
