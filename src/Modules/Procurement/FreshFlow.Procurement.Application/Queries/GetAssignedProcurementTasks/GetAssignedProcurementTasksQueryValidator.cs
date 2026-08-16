using FluentValidation;

namespace FreshFlow.Procurement.Application.Queries.GetAssignedProcurementTasks;

internal sealed class GetAssignedProcurementTasksQueryValidator
    : AbstractValidator<GetAssignedProcurementTasksQuery>
{
    public GetAssignedProcurementTasksQueryValidator()
    {
        RuleFor(query => query.AgentUserId).NotEmpty();
        RuleFor(query => query.Page).GreaterThan(0);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
    }
}
