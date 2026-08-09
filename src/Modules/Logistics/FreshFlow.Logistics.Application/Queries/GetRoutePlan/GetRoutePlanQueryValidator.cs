using FluentValidation;

namespace FreshFlow.Logistics.Application.Queries.GetRoutePlan;

internal sealed class GetRoutePlanQueryValidator : AbstractValidator<GetRoutePlanQuery>
{
    public GetRoutePlanQueryValidator() => RuleFor(x => x.PlanId).NotEmpty();
}
