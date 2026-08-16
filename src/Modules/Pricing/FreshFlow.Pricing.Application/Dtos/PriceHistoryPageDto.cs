namespace FreshFlow.Pricing.Application.Dtos;

/// <summary>
/// Cursor-paginated price change history for a single market product.
/// Maps to { data: [...], meta: { pageSize, nextCursor } } via ApiResponse.OkPaged.
/// </summary>
public sealed record PriceHistoryPageDto(
    IReadOnlyList<PriceHistoryItemDto> Items,
    int PageSize,
    string? NextCursor);
