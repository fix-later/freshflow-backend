using MediatR;

namespace FreshFlow.Contracts;

public sealed record ProcurementPurchaseConfirmedIntegrationEvent(
    Guid BatchId,
    Guid MarketId,
    Guid AgentUserId,
    IReadOnlyDictionary<Guid, decimal> ActualUnitPrices,
    DateTime ConfirmedAt) : INotification;
