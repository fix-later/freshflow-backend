using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Pricing.Application.Queries.SearchMarketProducts;

public sealed record SearchMarketProductsQuery(
    Guid MarketId,
    string SearchText,
    string? Category = null,
    bool InStockOnly = false,
    string? Cursor = null,
    int PageSize = 20)
    : IQuery<SearchMarketProductsResultDto>;
