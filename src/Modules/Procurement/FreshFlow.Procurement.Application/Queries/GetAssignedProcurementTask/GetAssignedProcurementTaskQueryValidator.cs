using FluentValidation;

namespace FreshFlow.Procurement.Application.Queries.GetAssignedProcurementTask;

internal sealed class GetAssignedProcurementTaskQueryValidator
    : AbstractValidator<GetAssignedProcurementTaskQuery>
{
    public GetAssignedProcurementTaskQueryValidator()
    {
        RuleFor(query => query.AgentUserId).NotEmpty();
        RuleFor(query => query.BatchId).NotEmpty();
    }
}
