using FluentValidation;

namespace FreshFlow.Pricing.Application.Queries.GetPriceChangeHistory;

internal sealed class GetPriceChangeHistoryQueryValidator
    : AbstractValidator<GetPriceChangeHistoryQuery>
{
    public GetPriceChangeHistoryQueryValidator()
    {
        RuleFor(x => x.MarketId).NotEmpty();
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.PageSize).InclusiveBetween(1, 200);

        // from and to are validated separately for format (in the controller); here
        // we enforce logical consistency: from must be earlier than or equal to to.
        RuleFor(x => x)
            .Must(q => q.From is null || q.To is null || q.From.Value <= q.To.Value)
            .WithErrorCode("VALIDATION_ERROR")
            .WithMessage("'from' must be earlier than or equal to 'to'.");
    }
}
