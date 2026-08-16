using FluentValidation;

namespace FreshFlow.Logistics.Application.Commands.ApproveRoutePlan;

internal sealed class ApproveRoutePlanCommandValidator : AbstractValidator<ApproveRoutePlanCommand>
{
    public ApproveRoutePlanCommandValidator() => RuleFor(x => x.PlanId).NotEmpty();
}
