using MediatR;

namespace FreshFlow.Contracts;

public sealed record ProcurementBatchCancelledIntegrationEvent(
    Guid BatchId,
    Guid MarketId,
    string? Reason,
    DateTime CancelledAt,
    IReadOnlyList<Guid> CoveredOrderIds) : INotification;
