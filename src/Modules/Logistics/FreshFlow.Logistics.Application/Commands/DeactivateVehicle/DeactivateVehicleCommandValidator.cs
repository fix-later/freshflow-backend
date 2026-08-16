using FluentValidation;

namespace FreshFlow.Logistics.Application.Commands.DeactivateVehicle;

internal sealed class DeactivateVehicleCommandValidator : AbstractValidator<DeactivateVehicleCommand>
{
    public DeactivateVehicleCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}
