using FluentValidation;

namespace FreshFlow.Hub.Application.Commands.CreateHub;

internal sealed class CreateHubCommandValidator : AbstractValidator<CreateHubCommand>
{
    public CreateHubCommandValidator()
    {
        RuleFor(x => x.MarketId).NotEmpty();

        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Address)
            .MaximumLength(500);

        RuleFor(x => x.CapacityKg)
            .GreaterThan(0);

        RuleFor(x => x.Latitude)
            .InclusiveBetween(-90, 90)
            .When(x => x.Latitude.HasValue);

        RuleFor(x => x.Longitude)
            .InclusiveBetween(-180, 180)
            .When(x => x.Longitude.HasValue);
    }
}
