namespace FreshFlow.Orders.Application.Dtos;

public sealed record OrderDto(
    Guid OrderId,
    Guid RestaurantId,
    string Status,
    string PaymentStatus,
    DateTime? ScheduledFor,
    decimal TotalAmount,
    string? Notes,
    IReadOnlyList<OrderItemDto> Items);
