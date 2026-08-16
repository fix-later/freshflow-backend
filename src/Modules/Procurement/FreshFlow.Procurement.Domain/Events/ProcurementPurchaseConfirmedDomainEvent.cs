using FreshFlow.SharedKernel.Domain;

namespace FreshFlow.Procurement.Domain.Events;

public sealed record ProcurementPurchaseConfirmedDomainEvent(
    Guid BatchId,
    Guid MarketId,
    Guid AgentUserId,
    IReadOnlyDictionary<Guid, decimal> ActualUnitPrices,
    DateTime ConfirmedAt) : IDomainEvent;
