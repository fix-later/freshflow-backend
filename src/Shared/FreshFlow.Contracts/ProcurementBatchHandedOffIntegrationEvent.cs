using MediatR;

namespace FreshFlow.Contracts;

public sealed record ProcurementBatchHandedOffIntegrationEvent(
    Guid BatchId,
    Guid MarketId,
    Guid? HubId,
    DateTime HandedOffAt,
    IReadOnlyList<Guid> CoveredOrderIds,
    Guid? HandedOffByUserId = null) : INotification;
