namespace FreshFlow.Hub.Application.Dtos;

public sealed record HubSortingProgressDto(
    Guid HubId,
    DateOnly ServiceDate,
    Guid? RouteId,
    Guid OrderItemId,
    decimal SortedQuantityKg,
    string Status,
    Guid? SortedByUserId,
    DateTime? SortedAt);
