using FluentValidation;

namespace FreshFlow.Orders.Application.Commands.UpdateOperationalSettings;

internal sealed class UpdateOperationalSettingsCommandValidator : AbstractValidator<UpdateOperationalSettingsCommand>
{
    private static readonly string[] AllowedRouteTypes = ["hub_relay", "direct"];

    public UpdateOperationalSettingsCommandValidator()
    {
        RuleFor(c => c.DefaultRouteType)
            .Must(v => AllowedRouteTypes.Contains(v))
            .WithMessage("DefaultRouteType must be one of: hub_relay, direct.");
    }
}
