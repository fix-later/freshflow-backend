using FreshFlow.SharedKernel.Domain;

namespace FreshFlow.Hub.Domain.Events;

public sealed record HubDiscrepancyRecordedDomainEvent(
    Guid DiscrepancyId,
    Guid HubId,
    Guid InboundEventId,
    Guid OrderId,
    Guid OrderItemId,
    decimal AffectedQuantity,
    string ConditionStatus,
    DateTime OccurredAt) : IDomainEvent;
