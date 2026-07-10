using MediatR;

namespace FreshFlow.Contracts;

public sealed record HubDiscrepancyRecordedIntegrationEvent(
    Guid DiscrepancyId,
    Guid HubId,
    Guid InboundEventId,
    Guid OrderId,
    Guid OrderItemId,
    decimal AffectedQuantity,
    string ConditionStatus,
    DateTime OccurredAt) : INotification;
