using FluentValidation;

namespace FreshFlow.Orders.Application.Queries.ListScheduledOrders;

internal sealed class ListScheduledOrdersQueryValidator : AbstractValidator<ListScheduledOrdersQuery>
{
    public ListScheduledOrdersQueryValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.Page).GreaterThan(0);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}
