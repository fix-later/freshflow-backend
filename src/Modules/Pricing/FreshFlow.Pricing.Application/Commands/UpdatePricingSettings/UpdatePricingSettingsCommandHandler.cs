using FreshFlow.Pricing.Application.Abstractions;
using FreshFlow.Pricing.Application.Dtos;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Pricing.Application.Commands.UpdatePricingSettings;

internal sealed class UpdatePricingSettingsCommandHandler(IPricingSettingsRepository settings)
    : IRequestHandler<UpdatePricingSettingsCommand, Result<PricingSettingsDto>>
{
    public async Task<Result<PricingSettingsDto>> Handle(UpdatePricingSettingsCommand request, CancellationToken ct)
    {
        var updated = await settings.UpsertAsync(request.PriceAlertThresholdPercent, ct);
        return Result<PricingSettingsDto>.Success(
            new PricingSettingsDto(updated.PriceAlertThresholdPercent, updated.UpdatedAt));
    }
}
