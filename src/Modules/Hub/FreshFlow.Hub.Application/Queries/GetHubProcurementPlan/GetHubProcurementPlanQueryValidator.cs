using FluentValidation;

namespace FreshFlow.Hub.Application.Queries.GetHubProcurementPlan;

internal sealed class GetHubProcurementPlanQueryValidator
    : AbstractValidator<GetHubProcurementPlanQuery>
{
    public GetHubProcurementPlanQueryValidator()
    {
        RuleFor(query => query.HubId).NotEmpty();
        RuleFor(query => query.Date).NotEmpty();
        RuleFor(query => query.ActorUserId).NotEmpty();
    }
}
