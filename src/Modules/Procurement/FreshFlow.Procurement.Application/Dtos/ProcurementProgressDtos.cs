namespace FreshFlow.Procurement.Application.Dtos;

public sealed record ProcurementProgressDto(
    ProcurementProgressSummaryDto Summary,
    IReadOnlyList<ProcurementBatchProgressDto> Batches);

public sealed record ProcurementProgressSummaryDto(
    DateOnly? BatchDate,
    int TotalBatches,
    IReadOnlyDictionary<string, int> StatusCounts,
    int TotalItems,
    int ItemsPurchased,
    int ItemsPending,
    int OpenExceptions);

public sealed record ProcurementBatchProgressDto(
    Guid BatchId,
    Guid MarketId,
    string Status,
    Guid? AssignedAgentUserId,
    Guid? HubId,
    int ItemsTotal,
    int ItemsPurchased,
    int ItemsPending,
    int ExceptionCount,
    int OrderCount,
    DateTime? ManifestedAt,
    DateTime? AssignedAt,
    DateTime? HandedOffAt);
