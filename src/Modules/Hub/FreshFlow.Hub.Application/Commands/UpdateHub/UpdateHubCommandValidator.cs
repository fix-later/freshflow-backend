using FluentValidation;

namespace FreshFlow.Hub.Application.Commands.UpdateHub;

internal sealed class UpdateHubCommandValidator : AbstractValidator<UpdateHubCommand>
{
    public UpdateHubCommandValidator()
    {
        RuleFor(x => x.HubId).NotEmpty();

        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Address)
            .MaximumLength(500);

        RuleFor(x => x.CapacityKg)
            .GreaterThan(0);

        RuleFor(x => x.ManagedBy)
            .NotEqual(Guid.Empty)
            .When(x => x.ManagedBy.HasValue);

        RuleFor(x => x.Latitude)
            .InclusiveBetween(-90, 90)
            .When(x => x.Latitude.HasValue);

        RuleFor(x => x.Longitude)
            .InclusiveBetween(-180, 180)
            .When(x => x.Longitude.HasValue);
    }
}
