using FluentValidation;

namespace FreshFlow.Orders.Application.Commands.CancelScheduledOrder;

internal sealed class CancelScheduledOrderCommandValidator : AbstractValidator<CancelScheduledOrderCommand>
{
    public CancelScheduledOrderCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.ScheduledOrderId).NotEmpty();
    }
}
