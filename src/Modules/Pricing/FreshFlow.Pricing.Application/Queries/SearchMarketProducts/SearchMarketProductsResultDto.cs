namespace FreshFlow.Pricing.Application.Queries.SearchMarketProducts;

/// <summary>
/// Cursor-paginated result for the market product free-text search.
/// </summary>
public sealed record SearchMarketProductsResultDto(
    IReadOnlyList<MarketProductSearchItemDto> Items,
    string? NextCursor);
