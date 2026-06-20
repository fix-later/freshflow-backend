using FluentValidation;

namespace FreshFlow.Orders.Application.Commands.SetRestaurantCreditLimit;

internal sealed class SetRestaurantCreditLimitCommandValidator : AbstractValidator<SetRestaurantCreditLimitCommand>
{
    public SetRestaurantCreditLimitCommandValidator()
    {
        RuleFor(c => c.RestaurantId).NotEmpty();
        RuleFor(c => c.CreditLimit).GreaterThanOrEqualTo(0m);
        RuleFor(c => c.Note).MaximumLength(500);
    }
}
