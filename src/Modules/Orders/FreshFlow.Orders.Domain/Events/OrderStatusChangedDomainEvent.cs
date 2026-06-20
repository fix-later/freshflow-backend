using FreshFlow.Orders.Domain.Enums;
using FreshFlow.SharedKernel.Domain;

namespace FreshFlow.Orders.Domain.Events;

public sealed record OrderStatusChangedDomainEvent(
    Guid OrderId,
    Guid RestaurantId,
    OrderStatus PreviousStatus,
    OrderStatus NewStatus,
    DateTime OccurredAt) : IDomainEvent;
