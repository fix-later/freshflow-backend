using FluentValidation;

namespace FreshFlow.Logistics.Application.Commands.PlanRoutes;

internal sealed class PlanRoutesCommandValidator : AbstractValidator<PlanRoutesCommand>
{
    private static readonly HashSet<string> Criteria =
        new(StringComparer.OrdinalIgnoreCase) { "DISTANCE", "TIME", "COST" };

    public PlanRoutesCommandValidator()
    {
        RuleFor(command => command.MarketSessionId).NotEmpty();
        RuleFor(command => command.OptimizationCriteria)
            .Must(criteria => string.IsNullOrWhiteSpace(criteria) || Criteria.Contains(criteria))
            .WithMessage("OptimizationCriteria must be DISTANCE, TIME, or COST.");
    }
}
