using FreshFlow.Pricing.Application.Abstractions;
using FreshFlow.Pricing.Application.Dtos;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Pricing.Application.Queries.GetPricingSettings;

internal sealed class GetPricingSettingsQueryHandler(IPricingSettingsRepository settings)
    : IRequestHandler<GetPricingSettingsQuery, Result<PricingSettingsDto>>
{
    public async Task<Result<PricingSettingsDto>> Handle(GetPricingSettingsQuery request, CancellationToken ct)
    {
        var current = await settings.GetAsync(ct);
        return Result<PricingSettingsDto>.Success(
            new PricingSettingsDto(current.PriceAlertThresholdPercent, current.UpdatedAt));
    }
}
