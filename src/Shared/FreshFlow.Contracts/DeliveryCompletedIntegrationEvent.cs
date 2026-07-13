using MediatR;

namespace FreshFlow.Contracts;

public sealed record DeliveryCompletedIntegrationEvent(
    Guid OrderId,
    Guid RouteId,
    DateTime ActualArrivalAt,
    DateTime OccurredAt) : INotification;
