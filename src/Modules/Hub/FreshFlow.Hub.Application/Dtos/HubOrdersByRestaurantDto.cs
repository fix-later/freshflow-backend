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
    int Quantity,
    decimal? CapacityKg);
