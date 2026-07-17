using FreshFlow.SharedKernel.Domain;

namespace FreshFlow.Procurement.Domain.Events;

public sealed record ProcurementBatchCancelledDomainEvent(
    Guid BatchId,
    Guid MarketId,
    string? Reason,
    DateTime CancelledAt,
    IReadOnlyList<Guid> CoveredOrderIds) : IDomainEvent;
