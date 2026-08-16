using FluentValidation;

namespace FreshFlow.Catalog.Application.Commands.Units.Update;

internal sealed class UpdateUnitCommandValidator : AbstractValidator<UpdateUnitCommand>
{
    public UpdateUnitCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();

        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.Abbreviation)
            .MaximumLength(20)
            .When(x => x.Abbreviation is not null);
    }
}
