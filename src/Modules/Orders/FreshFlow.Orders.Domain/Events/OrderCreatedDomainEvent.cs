using FreshFlow.SharedKernel.Domain;

namespace FreshFlow.Orders.Domain.Events;

public sealed record OrderCreatedDomainEvent(
    Guid OrderId,
    Guid RestaurantId,
    DateTime OccurredAt) : IDomainEvent;
