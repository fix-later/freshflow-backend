using MediatR;

namespace FreshFlow.Contracts;

public sealed record DeliveryStartedIntegrationEvent(
    Guid RouteId,
    IReadOnlyList<Guid> OrderIds,
    DateTime OccurredAt) : INotification;
