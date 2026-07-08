namespace FreshFlow.Orders.Application.Dtos;

/// <summary>
/// Cursor-paginated credit transaction (balance ledger) history for a restaurant.
/// Maps to { data: [...], meta: { pageSize, nextCursor } } via ApiResponse.OkPaged.
/// </summary>
public sealed record CreditTransactionPageDto(
    IReadOnlyList<CreditTransactionDto> Items,
    int PageSize,
    string? NextCursor);
