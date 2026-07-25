using FreshFlow.Logistics.Application.Abstractions;

namespace FreshFlow.Logistics.UnitTests.TestDoubles;

internal sealed class InMemoryOrderStatusReader : IOrderStatusReader
{
    private readonly Dictionary<Guid, OrderStatusLookupDto> _orders = [];

    public void Add(Guid orderId, string status, Guid restaurantId) =>
        _orders[orderId] = new OrderStatusLookupDto(orderId, status, restaurantId);

    public Task<OrderStatusLookupDto?> FindByIdAsync(Guid orderId, CancellationToken ct) =>
        Task.FromResult(_orders.GetValueOrDefault(orderId));

    public Task<IReadOnlyList<OrderStatusLookupDto>> ListByRestaurantsAndStatusAsync(
        IReadOnlyCollection<Guid> restaurantIds, string status, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<OrderStatusLookupDto>>(
            _orders.Values
                .Where(o => o.Status == status && restaurantIds.Contains(o.RestaurantId))
                .ToList());
}
