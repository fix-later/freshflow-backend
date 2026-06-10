using FluentValidation;

namespace FreshFlow.Catalog.Application.Commands.Units.Create;

internal sealed class CreateUnitCommandValidator : AbstractValidator<CreateUnitCommand>
{
    public CreateUnitCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.Abbreviation)
            .MaximumLength(20)
            .When(x => x.Abbreviation is not null);
    }
}
