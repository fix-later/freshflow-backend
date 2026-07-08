using FluentValidation;

namespace FreshFlow.Orders.Application.Commands.SettleRestaurantCredit;

internal sealed class SettleRestaurantCreditCommandValidator : AbstractValidator<SettleRestaurantCreditCommand>
{
    public SettleRestaurantCreditCommandValidator()
    {
        RuleFor(c => c.RestaurantId).NotEmpty();
        RuleFor(c => c.Amount).GreaterThan(0m);
        RuleFor(c => c.PaymentMethod).IsInEnum();
        RuleFor(c => c.Reference).MaximumLength(200);
        RuleFor(c => c.Note).MaximumLength(500);
    }
}
