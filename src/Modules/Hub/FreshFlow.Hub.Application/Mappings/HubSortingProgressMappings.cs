using FreshFlow.Hub.Application.Abstractions;
using FreshFlow.Hub.Application.Dtos;
using FreshFlow.Hub.Domain.Entities;

namespace FreshFlow.Hub.Application.Mappings;

internal static class HubSortingProgressMappings
{
    public static HubSortingProgressDto ToDto(
        this HubSortingProgress progress,
        OrderLookupDto? order = null) =>
        new(
            progress.HubId,
            progress.ServiceDate,
            progress.RouteId,
            progress.OrderItemId,
            progress.SortedQuantityKg,
            progress.Status,
            progress.SortedByUserId,
            progress.SortedAt,
            order?.OrderId,
            order is null ? null : order.ActualQuantity ?? order.Quantity,
            order is null
                ? null
                : Math.Max((order.ActualQuantity ?? order.Quantity) - progress.SortedQuantityKg, 0m));
}
