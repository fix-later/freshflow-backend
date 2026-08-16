using FreshFlow.Pricing.Application.Dtos;

namespace FreshFlow.Pricing.Application.Queries.SearchMarketProducts;

/// <summary>
/// Single item in the market product free-text search result.
/// Returned by <c>SearchMarketProductsQuery</c> (AI Assistant thin query, UC-ASSIST-01).
/// </summary>
public sealed record MarketProductSearchItemDto(
    Guid MarketProductId,
    Guid ProductId,
    string ProductName,
    string? Category,
    decimal CurrentPrice,

    // current − reserved, matching IMarketProductReader.FindAsync semantics.
    int AvailableQuantity,
    SellingUnitDto SellingUnit);
