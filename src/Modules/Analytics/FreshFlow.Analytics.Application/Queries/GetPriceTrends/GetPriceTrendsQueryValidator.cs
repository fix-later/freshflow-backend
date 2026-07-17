using FluentValidation;

namespace FreshFlow.Analytics.Application.Queries.GetPriceTrends;

internal sealed class GetPriceTrendsQueryValidator : AbstractValidator<GetPriceTrendsQuery>
{
    private static readonly DateOnly LatestDateWith24MonthsRemaining =
        DateOnly.MaxValue.AddMonths(-24);

    public GetPriceTrendsQueryValidator()
    {
        RuleFor(query => query.MarketProductIds)
            .NotEmpty()
            .WithErrorCode("VALIDATION_ERROR");

        RuleFor(query => query.MarketProductIds)
            .Must(ids => ids is null || ids.Count <= 10)
            .WithErrorCode("VALIDATION_ERROR")
            .WithMessage("At most 10 'marketProductId' values are allowed.");

        RuleForEach(query => query.MarketProductIds)
            .NotEmpty()
            .WithErrorCode("VALIDATION_ERROR");

        RuleFor(query => query)
            .Must(query => query.From <= query.To)
            .WithErrorCode("VALIDATION_ERROR")
            .WithMessage("'from' must be earlier than or equal to 'to'.");

        RuleFor(query => query)
            .Must(IsWithin24Months)
            .WithErrorCode("VALIDATION_ERROR")
            .WithMessage("The requested range cannot exceed 24 months.");

        RuleFor(query => query.Interval)
            .Must(interval => interval is null ||
                interval.Equals("hourly", StringComparison.OrdinalIgnoreCase) ||
                interval.Equals("daily", StringComparison.OrdinalIgnoreCase))
            .WithErrorCode("VALIDATION_ERROR")
            .WithMessage("'interval' must be either 'hourly' or 'daily'.");
    }

    private static bool IsWithin24Months(GetPriceTrendsQuery query) =>
        query.From > query.To ||
        query.From > LatestDateWith24MonthsRemaining ||
        query.To <= query.From.AddMonths(24);
}
