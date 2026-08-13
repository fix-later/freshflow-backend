namespace FreshFlow.Procurement.Application.Dtos;

public sealed record ProcurementBatchOverviewDto(
    Guid BatchId,
    string? Code,
    Guid MarketId,
    Guid? HubId,
    DateOnly BatchDate,
    string Status,
    DateTime? ManifestedAt,
    DateTime? HandedOffAt,
    DateTime? CompletedAt,
    DateTime? CancelledAt,
    string? CancellationReason,
    int TotalItemCount,
    int ItemsPurchased,
    int ItemsPending,
    int ExceptionCount,
    IReadOnlyList<BatchAgentPerformanceDto> Agents,
    IReadOnlyList<BatchOrderStatusDto> Orders);

// ponytail: IDs cover phase 1; add an agent-profile reader when the UI needs display names.
public sealed record BatchAgentPerformanceDto(
    Guid AgentUserId,
    int ItemsAssigned,
    int ItemsPurchased,
    int ItemsPending,
    int ExceptionsReported,
    decimal? ReferenceCostTotal,
    decimal? ActualCostTotal,
    decimal? VarianceTotal);

public sealed record BatchOrderStatusDto(Guid OrderId, string? Status);
