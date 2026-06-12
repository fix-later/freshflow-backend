using FreshFlow.Pricing.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Pricing.Application.Queries.GetMarketProducts;

public sealed record GetMarketProductsQuery(
    Guid MarketId,
    string? Category = null,
    string? Cursor = null,
    int PageSize = 20)
    : IQuery<MarketProductPageDto>;
