using FreshFlow.Orders.Domain.Entities;
using FreshFlow.Orders.Domain.Enums;

namespace FreshFlow.Orders.Application.Dtos;

public sealed record ScheduledOrderDto(
    Guid ScheduledOrderId,
    Guid RestaurantId,
    string RecurrenceType,
    DateTime FirstRunAt,
    DateTime? LastExecutedAt,
    DateTime? CancelledAt,
    string? Notes,
    Guid? DeliveryAddressId,
    IReadOnlyList<ScheduledOrderItemDto> Items,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    Guid? MarketId = null);

public sealed record ScheduledOrderItemDto(Guid MarketProductId, int Quantity);

public sealed record ScheduledOrderListResponseDto(
    IReadOnlyList<ScheduledOrderDto> Data,
    OrderPaginationMeta Meta);

public sealed record ScheduledOrderGenerationResultDto(
    int ScheduledOrderCount,
    int CreatedOrderCount,
    int MissedExecutionCount,
    DateTime GeneratedUntilUtc);

internal static class ScheduledOrderDtoMapper
{
    public static ScheduledOrderDto ToDto(ScheduledOrder scheduledOrder) => new(
        scheduledOrder.Id,
        scheduledOrder.RestaurantId,
        ToApiRecurrenceType(scheduledOrder.RecurrenceType),
        scheduledOrder.FirstRunAt,
        scheduledOrder.LastExecutedAt,
        scheduledOrder.CancelledAt,
        scheduledOrder.Notes,
        scheduledOrder.DeliveryAddressId,
        scheduledOrder.Items
            .Select(item => new ScheduledOrderItemDto(item.MarketProductId, item.Quantity))
            .ToList(),
        scheduledOrder.CreatedAt,
        scheduledOrder.UpdatedAt,
        scheduledOrder.MarketId);

    public static string ToApiRecurrenceType(RecurrenceType recurrenceType) => recurrenceType switch
    {
        RecurrenceType.Daily => "daily",
        RecurrenceType.Weekly => "weekly",
        _ => recurrenceType.ToString().ToLowerInvariant()
    };
}
