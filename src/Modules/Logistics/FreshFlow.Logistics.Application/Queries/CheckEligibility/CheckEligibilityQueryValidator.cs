using FluentValidation;

namespace FreshFlow.Logistics.Application.Queries.CheckEligibility;

internal sealed class CheckEligibilityQueryValidator : AbstractValidator<CheckEligibilityQuery>
{
    public CheckEligibilityQueryValidator()
    {
        RuleFor(x => x.RouteId).NotEmpty();
        RuleFor(x => x.VehicleId).NotEmpty();
    }
}
