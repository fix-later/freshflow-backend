namespace FreshFlow.Hub.Application.Dtos;

public sealed record HubOrdersByRestaurantDto(
    Guid HubId,
    DateOnly ServiceDate,
    IReadOnlyList<HubRestaurantOrdersDto> Restaurants);

public sealed record HubRestaurantOrdersDto(
    Guid RestaurantId,
    string RestaurantName,
    int OrderCount,
    IReadOnlyList<HubSortingOrderDto> Orders);

public sealed record HubSortingOrderDto(
    Guid OrderId,
    string Status,
    IReadOnlyList<HubOrderSortingItemDto> Items);

public sealed record HubOrderSortingItemDto(
    Guid OrderItemId,
    string ProductName,
    Guid MarketProductId,
    Guid ProductId,
    string? Unit,
    int OrderedQuantity,
    decimal RequiredQuantity,
    string? PackingCode,
    decimal? PackingCapacityKg,
    int? PackageCount,
    decimal SortedQuantityKg,
    decimal RemainingQuantityKg,
    string Status);

public sealed record HubOrderLineDto(
    Guid OrderId,
    Guid OrderItemId,
    string ProductName,
    Guid MarketProductId,
    Guid ProductId,
    string? Unit,
    int OrderedQuantity,
    decimal? ActualQuantity,
    string? PackingCode,
    decimal? CapacityKg);
