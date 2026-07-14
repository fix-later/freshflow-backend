using MediatR;

namespace FreshFlow.Contracts;

public sealed record ProcurementBatchBuiltIntegrationEvent(
    Guid BatchId,
    Guid MarketId,
    DateOnly BatchDate,
    IReadOnlyList<Guid> CoveredOrderIds) : INotification;
