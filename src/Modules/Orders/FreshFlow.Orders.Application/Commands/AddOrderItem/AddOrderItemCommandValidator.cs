using FluentValidation;

namespace FreshFlow.Orders.Application.Commands.AddOrderItem;

internal sealed class AddOrderItemCommandValidator : AbstractValidator<AddOrderItemCommand>
{
    public AddOrderItemCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.OrderId).NotEmpty();
        RuleFor(x => x.MarketProductId).NotEmpty();
        RuleFor(x => x.Quantity).GreaterThan(0);
    }
}
