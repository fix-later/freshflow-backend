namespace FreshFlow.Hub.Application.Dtos;

public sealed record HubSortingProgressDto(
    Guid RouteId,
    Guid OrderItemId,
    decimal SortedQuantityKg,
    string Status,
    Guid? SortedByUserId,
    DateTime? SortedAt);
