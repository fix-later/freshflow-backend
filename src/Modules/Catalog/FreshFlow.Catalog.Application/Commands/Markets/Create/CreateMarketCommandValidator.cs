using FluentValidation;

namespace FreshFlow.Catalog.Application.Commands.Markets.Create;

public sealed class CreateMarketCommandValidator : AbstractValidator<CreateMarketCommand>
{
    public CreateMarketCommandValidator()
    {
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
        RuleFor(x => x.ImageUrl)
            .MaximumLength(512)
            .Must(url =>
                Uri.TryCreate(url, UriKind.Absolute, out var u) &&
                (u.Scheme == Uri.UriSchemeHttps || u.Scheme == Uri.UriSchemeHttp))
            .WithMessage("ImageUrl must be a valid absolute HTTPS or HTTP URL.")
            .When(x => x.ImageUrl is not null);

        RuleFor(x => x.Description)
            .MaximumLength(2000)
            .When(x => x.Description is not null);
    }
}
