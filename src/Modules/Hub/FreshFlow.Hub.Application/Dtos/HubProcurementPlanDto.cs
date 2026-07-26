namespace FreshFlow.Hub.Application.Dtos;

public sealed record HubProcurementPlanDto(
    Guid HubId,
    DateOnly Date,
    IReadOnlyList<HubProcurementBatchDto> Batches);

public sealed record HubProcurementBatchDto(
    Guid BatchId,
    Guid MarketId,
    string Status,
    DateTime? HandedOffAt,
    IReadOnlyList<Guid> OrderIds,
    IReadOnlyList<HubProcurementItemDto> Items);

public sealed record HubProcurementItemDto(
    Guid MarketProductId,
    string ProductName,
    int TargetQuantity,
    int? ActualQuantity,
    decimal? ActualUnitPrice,
    DateTime? PurchasedAt);
