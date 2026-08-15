using System.Globalization;
using FluentValidation;
using FreshFlow.Catalog.Application.Abstractions;
using Microsoft.Extensions.Configuration;

namespace FreshFlow.Catalog.Application.Commands.PackingCodes.Create;

internal sealed class CreatePackingCodeCommandValidator : AbstractValidator<CreatePackingCodeCommand>
{
    public CreatePackingCodeCommandValidator(
        IPackingCodeRepository packingCodes,
        IConfiguration config)
    {
        var maxLoadKg = ParseMaxLoadKg(config);

        RuleFor(x => x.Code)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .MaximumLength(50)
            .MustAsync(async (code, ct) =>
                !await packingCodes.CodeExistsAsync(code.Trim(), null, ct))
            .WithMessage("Packing code already exists.");

        RuleFor(x => x.Description)
            .MaximumLength(500)
            .When(x => x.Description is not null);

        RuleFor(x => x.CapacityKg)
            .GreaterThan(0)
            .Must(capacityKg => capacityKg == decimal.Truncate(capacityKg))
            .WithMessage("CapacityKg must be a whole number of kilograms.")
            .LessThanOrEqualTo(maxLoadKg);
    }

    private static decimal ParseMaxLoadKg(IConfiguration config) =>
        decimal.TryParse(config["Logistics:Box:MaxLoadKg"], NumberStyles.Number,
            CultureInfo.InvariantCulture, out var value) && value > 0
            ? value
            : 25m;
}
