using MediatR;

namespace FreshFlow.Contracts;

public sealed record RestaurantRefundIssuedIntegrationEvent(
    Guid RestaurantId,
    Guid OrderId,
    string OrderItemName,
    decimal AffectedQuantity,
    decimal RefundAmount,
    DateTime OccurredAt) : INotification;
