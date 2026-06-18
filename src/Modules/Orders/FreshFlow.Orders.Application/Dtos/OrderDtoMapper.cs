using FreshFlow.Orders.Domain.Entities;
using FreshFlow.Orders.Domain.Enums;

namespace FreshFlow.Orders.Application.Dtos;

internal static class OrderDtoMapper
{
    public static OrderDto ToDto(Order order) => new(
        order.Id,
        order.RestaurantId,
        ToApiStatus(order.Status),
        ToApiPaymentStatus(order.PaymentStatus),
        order.ScheduledFor,
        order.TotalAmount,
        order.Notes,
        order.Items
            .Select(i => new OrderItemDto(
                i.Id,
                i.MarketProductId,
                i.ProductNameSnapshot,
                i.Quantity,
                i.UnitPrice,
                i.Subtotal,
                i.ActualQuantity))
            .ToList(),
        order.OrderGroupId,
        order.ScheduledOrderId,
        order.CancelledAt,
        order.CancellationReason,
        order.CreatedAt,
        order.UpdatedAt);

    public static OrderListItemDto ToListItemDto(Order order) => new(
        order.Id,
        order.RestaurantId,
        order.OrderGroupId,
        order.ScheduledOrderId,
        ToApiStatus(order.Status),
        ToApiPaymentStatus(order.PaymentStatus),
        order.TotalAmount,
        order.Items.Count,
        order.ScheduledFor,
        order.CreatedAt);

    private static string ToApiStatus(OrderStatus status) => status switch
    {
        OrderStatus.Draft => "draft",
        OrderStatus.Confirmed => "confirmed",
        OrderStatus.Batched => "batched",
        OrderStatus.PickedUp => "picked_up",
        OrderStatus.AtHub => "at_hub",
        OrderStatus.Delivering => "delivering",
        OrderStatus.Delivered => "delivered",
        OrderStatus.Cancelled => "cancelled",
        _ => status.ToString().ToLowerInvariant()
    };

    private static string ToApiPaymentStatus(OrderPaymentStatus status) => status switch
    {
        OrderPaymentStatus.NotApplicable => "not_applicable",
        OrderPaymentStatus.Outstanding => "outstanding",
        OrderPaymentStatus.Settled => "settled",
        OrderPaymentStatus.Waived => "waived",
        _ => status.ToString().ToLowerInvariant()
    };
}
