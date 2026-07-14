using FluentValidation;

namespace FreshFlow.Pricing.Application.Commands.UpdatePricingSettings;

internal sealed class UpdatePricingSettingsCommandValidator : AbstractValidator<UpdatePricingSettingsCommand>
{
    public UpdatePricingSettingsCommandValidator()
    {
        RuleFor(c => c.PriceAlertThresholdPercent).InclusiveBetween(0.01m, 100m);
    }
}
