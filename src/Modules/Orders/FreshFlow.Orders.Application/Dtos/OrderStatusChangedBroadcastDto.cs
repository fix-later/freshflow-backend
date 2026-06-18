namespace FreshFlow.Orders.Application.Dtos;

/// <summary>Payload broadcast to SignalR clients when an order status changes.</summary>
public sealed record OrderStatusChangedBroadcastDto(
    Guid OrderId,
    Guid RestaurantId,
    string PreviousStatus,
    string NewStatus,
    DateTime ChangedAt,
    DateTime? EstimatedDeliveryAt);
