using MediatR;

namespace FreshFlow.Contracts;

public sealed record OrderCancelledIntegrationEvent(
    Guid OrderId,
    Guid RestaurantId,
    string? CancellationReason,
    DateTime OccurredAt) : INotification;
