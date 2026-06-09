using FluentValidation;

namespace FreshFlow.Auth.Application.Commands.UpdateRestaurantProfile;

public sealed class UpdateRestaurantProfileCommandValidator
    : AbstractValidator<UpdateRestaurantProfileCommand>
{
    public UpdateRestaurantProfileCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Address)
            .MaximumLength(500)
            .When(x => x.Address is not null);

        RuleFor(x => x.ContactPerson)
            .MaximumLength(200)
            .When(x => x.ContactPerson is not null);

        RuleFor(x => x.PickupEnd)
            .Must((cmd, pickupEnd) => pickupEnd!.Value > cmd.PickupStart!.Value)
            .WithMessage("Pickup end time must be after pickup start time.")
            .When(c => c.PickupStart.HasValue && c.PickupEnd.HasValue);
    }
}
