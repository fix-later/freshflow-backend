using FluentValidation;

namespace FreshFlow.Logistics.Application.Commands.CalculateRoute;

internal sealed class CalculateRouteCommandValidator : AbstractValidator<CalculateRouteCommand>
{
    private static readonly HashSet<string> OptimizationCriteria =
        new(StringComparer.OrdinalIgnoreCase) { "DISTANCE", "TIME", "COST" };

    public CalculateRouteCommandValidator()
    {
        RuleFor(x => x.SourceMarketIds).NotEmpty();
        RuleFor(x => x.DestinationRestaurantIds).NotEmpty();

        RuleFor(x => x.OptimizationCriteria)
            .Must(criteria => string.IsNullOrWhiteSpace(criteria) || OptimizationCriteria.Contains(criteria))
            .WithMessage("OptimizationCriteria must be DISTANCE, TIME, or COST.");
    }
}
