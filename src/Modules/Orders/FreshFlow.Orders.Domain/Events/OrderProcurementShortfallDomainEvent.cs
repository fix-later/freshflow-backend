using FreshFlow.SharedKernel.Domain;

namespace FreshFlow.Orders.Domain.Events;

public sealed record OrderProcurementShortfallDomainEvent(
    Guid OrderId,
    Guid RestaurantId,
    Guid OrderItemId,
    string ProductName,
    decimal ShortfallQuantity,
    decimal RefundAmount,
    DateTime OccurredAt) : IDomainEvent;
