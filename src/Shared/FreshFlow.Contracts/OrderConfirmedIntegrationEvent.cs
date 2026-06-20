using MediatR;

namespace FreshFlow.Contracts;

public sealed record OrderConfirmedIntegrationEvent(
    Guid OrderId,
    Guid RestaurantId,
    decimal TotalAmount,
    DateTime OccurredAt) : INotification;
