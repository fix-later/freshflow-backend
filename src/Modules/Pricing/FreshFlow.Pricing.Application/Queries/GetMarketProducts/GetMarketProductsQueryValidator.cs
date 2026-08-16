using FluentValidation;

namespace FreshFlow.Pricing.Application.Queries.GetMarketProducts;

internal sealed class GetMarketProductsQueryValidator : AbstractValidator<GetMarketProductsQuery>
{
    public GetMarketProductsQueryValidator()
    {
        RuleFor(x => x.MarketId).NotEmpty();
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}
