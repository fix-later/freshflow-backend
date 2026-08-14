namespace FreshFlow.Logistics.Application.Dtos;

/// <summary>
/// What to load onto the truck for a route, grouped by restaurant stop. Stops are ordered for
/// loading — furthest / last-delivered first (loaded innermost), nearest last (loaded outermost) —
/// i.e. the reverse of delivery <see cref="LoadingStopDto.StopOrder"/>.
/// </summary>
public sealed record LoadingManifestDto(
    Guid RouteId,
    string Status,
    DateOnly ServiceDate,
    IReadOnlyList<LoadingStopDto> Stops);

public sealed record LoadingStopDto(
    int StopOrder,
    Guid RestaurantId,
    string RestaurantName,
    IReadOnlyList<LoadingLineDto> Lines);

public sealed record LoadingLineDto(
    Guid OrderId,
    Guid OrderItemId,
    string ProductName,
    decimal Quantity,
    decimal? CapacityKg,
    Guid MarketProductId);
