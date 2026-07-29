using FluentValidation;

namespace FreshFlow.Logistics.Application.Commands.AssignVehicle;

internal sealed class AssignVehicleCommandValidator : AbstractValidator<AssignVehicleCommand>
{
    public AssignVehicleCommandValidator()
    {
        RuleFor(x => x.RouteId).NotEmpty();
        RuleFor(x => x.VehicleId).NotEmpty();
        RuleFor(x => x.DriverUserId).NotEmpty();
    }
}
