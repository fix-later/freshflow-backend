using FluentValidation;

namespace FreshFlow.Orders.Application.Queries.ListScheduledOrderInstances;

internal sealed class ListScheduledOrderInstancesQueryValidator
    : AbstractValidator<ListScheduledOrderInstancesQuery>
{
    public ListScheduledOrderInstancesQueryValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.ScheduledOrderId).NotEmpty();
        RuleFor(x => x.Page).GreaterThan(0);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}
