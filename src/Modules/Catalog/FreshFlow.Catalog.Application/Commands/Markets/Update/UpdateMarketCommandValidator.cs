using FluentValidation;

namespace FreshFlow.Catalog.Application.Commands.Markets.Update;

public sealed class UpdateMarketCommandValidator : AbstractValidator<UpdateMarketCommand>
{
    public UpdateMarketCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Market Id is required.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(200).WithMessage("Name must not exceed 200 characters.");

        When(x => x.Location is not null, () =>
            RuleFor(x => x.Location!)
                .MaximumLength(200).WithMessage("Location must not exceed 200 characters."));

        When(x => x.Address is not null, () =>
            RuleFor(x => x.Address!)
                .MaximumLength(500).WithMessage("Address must not exceed 500 characters."));

        When(x => x.Latitude.HasValue, () =>
            RuleFor(x => x.Latitude!.Value)
                .InclusiveBetween(-90m, 90m).WithMessage("Latitude must be between -90 and 90."));

        When(x => x.Longitude.HasValue, () =>
            RuleFor(x => x.Longitude!.Value)
                .InclusiveBetween(-180m, 180m).WithMessage("Longitude must be between -180 and 180."));
    }
}
