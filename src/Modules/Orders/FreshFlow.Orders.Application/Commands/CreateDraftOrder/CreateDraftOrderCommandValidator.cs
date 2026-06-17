using FluentValidation;

namespace FreshFlow.Orders.Application.Commands.CreateDraftOrder;

/// <summary>
/// Validates the structural constraints of <see cref="CreateDraftOrderCommand"/>.
///
/// Responsibility split:
/// • 400 (this validator) — missing UserId, missing/empty item list, malformed item fields.
/// • 422 (handler)        — restaurant not approved, invalid product, insufficient stock.
/// </summary>
internal sealed class CreateDraftOrderCommandValidator : AbstractValidator<CreateDraftOrderCommand>
{
    public CreateDraftOrderCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();

        RuleFor(x => x.Items)
            .NotEmpty()
            .WithMessage("An order must have at least one item.");

        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.MarketProductId).NotEmpty();
            item.RuleFor(i => i.Quantity).GreaterThan(0);
        });
    }
}
