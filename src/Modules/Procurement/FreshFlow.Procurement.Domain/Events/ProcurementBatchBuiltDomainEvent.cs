using FreshFlow.SharedKernel.Domain;

namespace FreshFlow.Procurement.Domain.Events;

public sealed record ProcurementBatchBuiltDomainEvent(
    Guid BatchId,
    Guid MarketId,
    DateOnly BatchDate,
    IReadOnlyList<Guid> CoveredOrderIds) : IDomainEvent;
