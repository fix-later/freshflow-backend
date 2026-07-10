using FluentValidation;

namespace FreshFlow.Catalog.Application.Commands.Products.Update;

internal sealed class UpdateProductCommandValidator : AbstractValidator<UpdateProductCommand>
{
    public UpdateProductCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();

        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.UnitId).NotEmpty();

        RuleFor(x => x.Description)
            .MaximumLength(1000)
            .When(x => x.Description is not null);

        RuleFor(x => x.CategoryId)
            .NotEqual(Guid.Empty)
            .WithMessage("CategoryId must not be an empty GUID.")
            .When(x => x.CategoryId.HasValue);

        RuleFor(x => x.ImageUrl)
            .MaximumLength(512)
            .Must(url =>
                Uri.TryCreate(url, UriKind.Absolute, out var u) &&
                (u.Scheme == Uri.UriSchemeHttps || u.Scheme == Uri.UriSchemeHttp))
            .WithMessage("ImageUrl must be a valid absolute HTTPS or HTTP URL.")
            .When(x => x.ImageUrl is not null);
    }
}
