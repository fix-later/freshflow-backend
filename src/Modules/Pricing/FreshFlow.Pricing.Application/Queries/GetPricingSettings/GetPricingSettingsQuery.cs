using FreshFlow.Pricing.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Pricing.Application.Queries.GetPricingSettings;

public sealed record GetPricingSettingsQuery : IQuery<PricingSettingsDto>;
