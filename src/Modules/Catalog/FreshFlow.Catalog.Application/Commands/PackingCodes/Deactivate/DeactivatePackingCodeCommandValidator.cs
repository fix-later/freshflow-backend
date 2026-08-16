using FluentValidation;

namespace FreshFlow.Catalog.Application.Commands.PackingCodes.Deactivate;

internal sealed class DeactivatePackingCodeCommandValidator
    : AbstractValidator<DeactivatePackingCodeCommand>
{
    public DeactivatePackingCodeCommandValidator() => RuleFor(x => x.Id).NotEmpty();
}
