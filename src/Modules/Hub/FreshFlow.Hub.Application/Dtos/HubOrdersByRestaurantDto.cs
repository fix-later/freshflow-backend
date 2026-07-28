namespace FreshFlow.Hub.Application.Dtos;

public sealed record HubOrdersByRestaurantDto(
    Guid HubId,
    DateOnly ServiceDate,
    IReadOnlyList<HubRestaurantOrdersDto> Restaurants);

public sealed record HubRestaurantOrdersDto(
    Guid RestaurantId,
    string RestaurantName,
    int OrderCount,
    IReadOnlyList<HubOrderLineDto> Lines);

public sealed record HubOrderLineDto(
    Guid OrderId,
    Guid OrderItemId,
    string ProductName,
    Guid MarketProductId,
    Guid ProductId,
    string? Unit,
    int OrderedQuantity,
    decimal? CapacityKg);
