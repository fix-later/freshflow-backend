using FluentValidation;

namespace FreshFlow.Orders.Application.Commands.RecordOrderItemActualQuantity;

internal sealed class RecordOrderItemActualQuantityCommandValidator
    : AbstractValidator<RecordOrderItemActualQuantityCommand>
{
    public RecordOrderItemActualQuantityCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.OrderId).NotEmpty();
        RuleFor(x => x.OrderItemId).NotEmpty();
        RuleFor(x => x.ActualQuantity).GreaterThanOrEqualTo(0m);
    }
}
