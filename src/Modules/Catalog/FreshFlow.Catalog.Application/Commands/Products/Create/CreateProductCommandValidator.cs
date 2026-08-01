using FluentValidation;
using FreshFlow.Catalog.Application.Abstractions;

namespace FreshFlow.Catalog.Application.Commands.Products.Create;

internal sealed class CreateProductCommandValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductCommandValidator(IPackingCodeRepository packingCodes)
    {
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

        RuleFor(x => x.MinimumOrderQuantity).GreaterThan(0);
        RuleFor(x => x.VatRate)
            .Must(rate => rate is null || new[] { "KCT", "0", "5", "8", "10" }
                .Contains(rate.Trim().ToUpperInvariant()))
            .WithMessage("VatRate must be KCT, 0, 5, 8, or 10.");

        RuleFor(x => x.PackingCodeId)
            .Cascade(CascadeMode.Stop)
            .NotEqual(Guid.Empty)
            .MustAsync(async (id, ct) =>
            {
                var packingCode = await packingCodes.FindByIdAsync(id!.Value, ct);
                return packingCode is { IsActive: true };
            })
            .WithMessage("PackingCodeId must reference an active packing code.")
            .When(x => x.PackingCodeId.HasValue);
    }
}
