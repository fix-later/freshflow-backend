using FreshFlow.SharedKernel.Domain;

namespace FreshFlow.Orders.Domain.Events;

public sealed record OrderCancelledDomainEvent(
    Guid OrderId,
    Guid RestaurantId,
    string? CancellationReason,
    DateTime OccurredAt) : IDomainEvent;
