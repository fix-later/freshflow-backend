using FluentValidation;
using FluentValidation.Results;

namespace FreshFlow.Logistics.Application.Commands.CalculateRoute;

internal sealed class CalculateRouteCommandValidator : AbstractValidator<CalculateRouteCommand>
{
    private static readonly HashSet<string> OptimizationCriteria =
        new(StringComparer.OrdinalIgnoreCase) { "DISTANCE", "TIME", "COST" };

    public CalculateRouteCommandValidator()
    {
        RuleFor(x => x).Custom((command, context) =>
        {
            if (command.HubIds.Count > 0 || command.CompareWithHub)
            {
                context.AddFailure(new ValidationFailure(
                    nameof(CalculateRouteCommand.HubIds),
                    "HUB_RELAY routing is not yet supported -- pending the Hub module (SCRUM-256).")
                {
                    ErrorCode = "HUB_RELAY_NOT_SUPPORTED"
                });
            }

            if (command.SourceMarketIds.Count + command.DestinationRestaurantIds.Count > 20)
            {
                context.AddFailure(new ValidationFailure(
                    nameof(CalculateRouteCommand.SourceMarketIds),
                    "A delivery route cannot contain more than 20 stops.")
                {
                    ErrorCode = "STOP_LIMIT_EXCEEDED"
                });
            }
        });

        RuleFor(x => x.SourceMarketIds).NotEmpty();
        RuleFor(x => x.DestinationRestaurantIds).NotEmpty();

        RuleFor(x => x.OptimizationCriteria)
            .Must(criteria => string.IsNullOrWhiteSpace(criteria) || OptimizationCriteria.Contains(criteria))
            .WithMessage("OptimizationCriteria must be DISTANCE, TIME, or COST.");
    }
}
