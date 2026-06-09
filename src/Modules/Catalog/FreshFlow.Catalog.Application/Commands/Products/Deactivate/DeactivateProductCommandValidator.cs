using FluentValidation;

namespace FreshFlow.Catalog.Application.Commands.Products.Deactivate;

internal sealed class DeactivateProductCommandValidator : AbstractValidator<DeactivateProductCommand>
{
    public DeactivateProductCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}
