using MediatR;

namespace FreshFlow.Contracts;

public sealed record DeliveryStopUpdatedIntegrationEvent(
    Guid OrderId,
    Guid RouteId,
    Guid DeliveryId,
    string Status,
    DateTime OccurredAt) : INotification;
