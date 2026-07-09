using FluentValidation;

namespace FreshFlow.Logistics.Application.Commands.DeliveryZones.Create;

internal sealed class CreateDeliveryZoneCommandValidator : AbstractValidator<CreateDeliveryZoneCommand>
{
    public CreateDeliveryZoneCommandValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty()
            .MaximumLength(50)
            .Matches("^[A-Za-z0-9_]+$")
            .WithMessage("Code may contain only letters, numbers, and underscores.");

        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Description)
            .MaximumLength(500);
    }
}
