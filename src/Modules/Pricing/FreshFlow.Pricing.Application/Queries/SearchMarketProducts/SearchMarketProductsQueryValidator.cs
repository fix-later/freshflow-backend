using FluentValidation;

namespace FreshFlow.Pricing.Application.Queries.SearchMarketProducts;

internal sealed class SearchMarketProductsQueryValidator : AbstractValidator<SearchMarketProductsQuery>
{
    public SearchMarketProductsQueryValidator()
    {
        RuleFor(x => x.MarketId).NotEmpty();
        RuleFor(x => x.SearchText).NotEmpty().MaximumLength(200);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}
