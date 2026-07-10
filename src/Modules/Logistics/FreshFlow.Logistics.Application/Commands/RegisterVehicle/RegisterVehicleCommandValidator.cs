using FluentValidation;
using FreshFlow.Logistics.Domain.Enums;

namespace FreshFlow.Logistics.Application.Commands.RegisterVehicle;

internal sealed class RegisterVehicleCommandValidator : AbstractValidator<RegisterVehicleCommand>
{
    public RegisterVehicleCommandValidator()
    {
        RuleFor(x => x.PlateNumber)
            .NotEmpty()
            .MaximumLength(20);

        RuleFor(x => x.CapacityKg)
            .GreaterThan(0);

        RuleFor(x => x.VehicleType)
            .Must(BeValidVehicleType)
            .WithMessage("VehicleType must be one of: van, truck, motorbike.");
    }

    private static bool BeValidVehicleType(string value) =>
        Enum.TryParse<VehicleType>(value, ignoreCase: true, out _);
}
