using FreshFlow.SharedKernel.Domain;

namespace FreshFlow.Procurement.Domain.Events;

public sealed record ProcurementBatchCompletedDomainEvent(
    Guid BatchId,
    Guid MarketId,
    DateTime CompletedAt) : IDomainEvent;
