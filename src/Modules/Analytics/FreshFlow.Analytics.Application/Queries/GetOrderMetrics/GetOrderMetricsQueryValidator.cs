using FluentValidation;

namespace FreshFlow.Analytics.Application.Queries.GetOrderMetrics;

internal sealed class GetOrderMetricsQueryValidator : AbstractValidator<GetOrderMetricsQuery>
{
    public GetOrderMetricsQueryValidator()
    {
        RuleFor(query => query)
            .Must(query => query.From <= query.To)
            .WithErrorCode("VALIDATION_ERROR")
            .WithMessage("'from' must be earlier than or equal to 'to'.");

        RuleFor(query => query)
            .Must(query => query.From > query.To || query.To.DayNumber - query.From.DayNumber <= 366)
            .WithErrorCode("VALIDATION_ERROR")
            .WithMessage("The requested range cannot exceed 366 days.");

        RuleFor(query => query.GroupBy)
            .Must(groupBy => groupBy is null ||
                groupBy.Equals("day", StringComparison.OrdinalIgnoreCase) ||
                groupBy.Equals("week", StringComparison.OrdinalIgnoreCase) ||
                groupBy.Equals("month", StringComparison.OrdinalIgnoreCase))
            .WithErrorCode("VALIDATION_ERROR")
            .WithMessage("'groupBy' must be 'day', 'week', or 'month'.");
    }
}
