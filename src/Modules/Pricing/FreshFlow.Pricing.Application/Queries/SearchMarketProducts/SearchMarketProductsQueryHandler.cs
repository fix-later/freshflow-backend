using FreshFlow.Pricing.Application.Abstractions;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Pricing.Application.Queries.SearchMarketProducts;

internal sealed class SearchMarketProductsQueryHandler(IMarketProductRepository repository)
    : IRequestHandler<SearchMarketProductsQuery, Result<SearchMarketProductsResultDto>>
{
    public async Task<Result<SearchMarketProductsResultDto>> Handle(
        SearchMarketProductsQuery request, CancellationToken ct)
    {
        var criteria = new MarketProductSearchCriteria(
            request.MarketId,
            request.SearchText,
            request.Category,
            request.InStockOnly,
            request.Cursor,
            request.PageSize);

        var (items, nextCursor) = await repository.SearchAsync(criteria, ct);

        return Result<SearchMarketProductsResultDto>.Success(
            new SearchMarketProductsResultDto(items, nextCursor));
    }
}
