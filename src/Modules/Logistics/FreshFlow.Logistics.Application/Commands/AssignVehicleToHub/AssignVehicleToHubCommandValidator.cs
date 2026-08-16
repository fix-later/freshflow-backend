using FluentValidation;

namespace FreshFlow.Logistics.Application.Commands.AssignVehicleToHub;

internal sealed class AssignVehicleToHubCommandValidator : AbstractValidator<AssignVehicleToHubCommand>
{
    public AssignVehicleToHubCommandValidator()
    {
        RuleFor(x => x.VehicleId).NotEmpty();
        RuleFor(x => x.HubId).NotEmpty();
    }
}
