using FluentValidation;

namespace FreshFlow.Logistics.Application.Commands.DeliveryZones.Deactivate;

internal sealed class DeactivateDeliveryZoneCommandValidator : AbstractValidator<DeactivateDeliveryZoneCommand>
{
    public DeactivateDeliveryZoneCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty();
    }
}
