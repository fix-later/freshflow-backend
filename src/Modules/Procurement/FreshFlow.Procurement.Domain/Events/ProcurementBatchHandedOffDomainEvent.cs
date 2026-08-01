using FreshFlow.SharedKernel.Domain;

namespace FreshFlow.Procurement.Domain.Events;

public sealed record ProcurementBatchHandedOffDomainEvent(
    Guid BatchId,
    Guid MarketId,
    Guid? HubId,
    DateTime HandedOffAt,
    IReadOnlyList<Guid> CoveredOrderIds,
    Guid? HandedOffByUserId = null,
    IReadOnlyList<ProcurementPurchasedLine>? PurchasedLines = null) : IDomainEvent;

public sealed record ProcurementPurchasedLine(
    Guid MarketProductId,
    int ActualQuantity,
    decimal? ActualUnitPrice);
