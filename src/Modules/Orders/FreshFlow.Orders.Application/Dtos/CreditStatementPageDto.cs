namespace FreshFlow.Orders.Application.Dtos;

/// <summary>
/// Cursor-paginated credit statement history for a restaurant.
/// Maps to { data: [...], meta: { pageSize, nextCursor } } via ApiResponse.OkPaged.
/// </summary>
public sealed record CreditStatementPageDto(
    IReadOnlyList<CreditStatementSummaryDto> Items,
    int PageSize,
    string? NextCursor);
