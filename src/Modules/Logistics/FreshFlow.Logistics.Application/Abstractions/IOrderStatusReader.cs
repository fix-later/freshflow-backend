namespace FreshFlow.Logistics.Application.Abstractions;

public interface IOrderStatusReader
{
    public Task<OrderStatusLookupDto?> FindByIdAsync(Guid orderId, CancellationToken ct);

    public Task<IReadOnlyList<OrderStatusLookupDto>> ListByRestaurantsAndStatusAsync(
        IReadOnlyCollection<Guid> restaurantIds,
        IReadOnlyCollection<string> statuses,
        CancellationToken ct,
        Guid? hubId = null,
        DateOnly? serviceDate = null);

    public Task<IReadOnlyList<(Guid RestaurantId, int OrderCount)>> ListRoutableRestaurantsAsync(
        DateOnly serviceDate,
        IReadOnlyCollection<string> statuses,
        CancellationToken ct);
}

public sealed record OrderStatusLookupDto(
    Guid OrderId,
    string Status,
    Guid RestaurantId,
    Guid? HubId = null,
    decimal? DeliveryLatitude = null,
    decimal? DeliveryLongitude = null);
