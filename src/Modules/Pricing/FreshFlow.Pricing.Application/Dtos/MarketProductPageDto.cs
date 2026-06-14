namespace FreshFlow.Pricing.Application.Dtos;

/// <summary>
/// Cursor-paginated result for market products.
/// Maps to { data: [...], meta: { pageSize, nextCursor } } in the API response.
/// </summary>
public sealed record MarketProductPageDto(
    IReadOnlyList<MarketProductItemDto> Items,
    int PageSize,
    string? NextCursor);
