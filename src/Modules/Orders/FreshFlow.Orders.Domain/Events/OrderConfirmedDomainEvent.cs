using FreshFlow.SharedKernel.Domain;

namespace FreshFlow.Orders.Domain.Events;

public sealed record OrderConfirmedDomainEvent(
    Guid OrderId,
    Guid RestaurantId,
    decimal TotalAmount,
    DateTime OccurredAt) : IDomainEvent;
