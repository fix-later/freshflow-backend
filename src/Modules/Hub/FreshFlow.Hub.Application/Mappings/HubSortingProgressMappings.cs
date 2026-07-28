using FreshFlow.Hub.Application.Dtos;
using FreshFlow.Hub.Domain.Entities;

namespace FreshFlow.Hub.Application.Mappings;

internal static class HubSortingProgressMappings
{
    public static HubSortingProgressDto ToDto(this HubSortingProgress progress) =>
        new(
            progress.HubId,
            progress.ServiceDate,
            progress.RouteId,
            progress.OrderItemId,
            progress.SortedQuantityKg,
            progress.Status,
            progress.SortedByUserId,
            progress.SortedAt);
}
