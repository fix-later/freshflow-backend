namespace FreshFlow.Orders.Application.Dtos;

public sealed record OrderDto(
    Guid OrderId,
    Guid RestaurantId,
    string Status,
    string PaymentStatus,
    DateTime? ScheduledFor,
    decimal TotalAmount,
    string? Notes,
    IReadOnlyList<OrderItemDto> Items,
    Guid? OrderGroupId,
    Guid? ScheduledOrderId,
    DateTime? CancelledAt,
    string? CancellationReason,
    DateTime? ConfirmedReceiptAt,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record OrderListItemDto(
    Guid OrderId,
    Guid RestaurantId,
    Guid? OrderGroupId,
    Guid? ScheduledOrderId,
    string Status,
    string PaymentStatus,
    decimal TotalAmount,
    int ItemCount,
    DateTime? ScheduledFor,
    DateTime CreatedAt);

public sealed record OrderListResponseDto(IReadOnlyList<OrderListItemDto> Data, OrderPaginationMeta Meta);

public sealed record OrderPaginationMeta(int Total, int Page, int PageSize);
