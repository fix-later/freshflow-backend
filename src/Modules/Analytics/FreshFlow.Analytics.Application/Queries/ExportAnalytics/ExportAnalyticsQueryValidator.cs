using FluentValidation;

namespace FreshFlow.Analytics.Application.Queries.ExportAnalytics;

internal sealed class ExportAnalyticsQueryValidator : AbstractValidator<ExportAnalyticsQuery>
{
    public ExportAnalyticsQueryValidator()
    {
        RuleFor(query => query.Dataset)
            .Must(dataset => dataset is not null &&
                (dataset.Equals("price-history", StringComparison.OrdinalIgnoreCase) ||
                 dataset.Equals("order-history", StringComparison.OrdinalIgnoreCase) ||
                 dataset.Equals("delivery-performance", StringComparison.OrdinalIgnoreCase)))
            .WithErrorCode("VALIDATION_ERROR")
            .WithMessage("'dataset' must be 'price-history', 'order-history', or 'delivery-performance'.");

        RuleFor(query => query.Format)
            .Must(format => format is null || format.Equals("csv", StringComparison.OrdinalIgnoreCase))
            .WithErrorCode("VALIDATION_ERROR")
            .WithMessage("'format' must be 'csv'.");

        RuleFor(query => query)
            .Must(query => query.From <= query.To)
            .WithErrorCode("VALIDATION_ERROR")
            .WithMessage("'from' must be earlier than or equal to 'to'.");

        RuleFor(query => query)
            .Must(query => query.From > query.To || query.To.DayNumber - query.From.DayNumber <= 366)
            .WithErrorCode("VALIDATION_ERROR")
            .WithMessage("The requested range cannot exceed 366 days.");

        When(query => IsPriceHistory(query.Dataset), () =>
        {
            RuleFor(query => query.MarketProductIds)
                .NotEmpty()
                .WithErrorCode("VALIDATION_ERROR");

            RuleFor(query => query.MarketProductIds)
                .Must(ids => ids is not null && ids.Count <= 10)
                .WithErrorCode("VALIDATION_ERROR")
                .WithMessage("Between 1 and 10 'marketProductId' values are required.");

            RuleForEach(query => query.MarketProductIds)
                .NotEmpty()
                .WithErrorCode("VALIDATION_ERROR");
        });
    }

    private static bool IsPriceHistory(string? dataset) =>
        dataset?.Equals("price-history", StringComparison.OrdinalIgnoreCase) == true;
}
