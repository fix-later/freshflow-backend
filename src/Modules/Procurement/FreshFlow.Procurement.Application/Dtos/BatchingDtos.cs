namespace FreshFlow.Procurement.Application.Dtos;

public sealed record BatchingResult(
    int BatchesCreated,
    int OrdersBatched,
    int ItemsAggregated,
    bool Skipped,
    string? Reason,
    IReadOnlyList<BatchingPreviewDto> Preview);

public sealed record BatchingPreviewDto(
    DateOnly BatchDate,
    Guid MarketId,
    string Status,
    IReadOnlyList<Guid> CoveredOrderIds,
    IReadOnlyList<BatchingPreviewItemDto> Items);

public sealed record BatchingPreviewItemDto(
    Guid MarketProductId,
    string ProductNameSnapshot,
    int TotalQuantity);
