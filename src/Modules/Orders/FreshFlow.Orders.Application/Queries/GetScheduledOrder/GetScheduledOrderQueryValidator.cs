using FluentValidation;

namespace FreshFlow.Orders.Application.Queries.GetScheduledOrder;

internal sealed class GetScheduledOrderQueryValidator : AbstractValidator<GetScheduledOrderQuery>
{
    public GetScheduledOrderQueryValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.ScheduledOrderId).NotEmpty();
    }
}
