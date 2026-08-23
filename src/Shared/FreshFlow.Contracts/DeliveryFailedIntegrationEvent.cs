using MediatR;

namespace FreshFlow.Contracts;

public sealed record DeliveryFailedIntegrationEvent(
    Guid OrderId,
    Guid RouteId,
    Guid DeliveryId,
    string Reason,
    DateTime OccurredAt) : INotification;
