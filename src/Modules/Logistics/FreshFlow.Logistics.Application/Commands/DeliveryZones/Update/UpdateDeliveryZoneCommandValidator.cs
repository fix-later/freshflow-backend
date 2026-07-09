using FluentValidation;

namespace FreshFlow.Logistics.Application.Commands.DeliveryZones.Update;

internal sealed class UpdateDeliveryZoneCommandValidator : AbstractValidator<UpdateDeliveryZoneCommand>
{
    public UpdateDeliveryZoneCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty();

        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Description)
            .MaximumLength(500);
    }
}
