using FreshFlow.Logistics.Application.Abstractions;

namespace FreshFlow.Logistics.UnitTests.TestDoubles;

internal sealed class InMemoryOrderStatusReader : IOrderStatusReader
{
    private readonly Dictionary<Guid, OrderStatusLookupDto> _orders = [];

    public void Add(Guid orderId, string status, Guid restaurantId, Guid? hubId = null) =>
        _orders[orderId] = new OrderStatusLookupDto(orderId, status, restaurantId, hubId);

    public Task<OrderStatusLookupDto?> FindByIdAsync(Guid orderId, CancellationToken ct) =>
        Task.FromResult(_orders.GetValueOrDefault(orderId));

    public Task<IReadOnlyList<OrderStatusLookupDto>> ListByRestaurantsAndStatusAsync(
        IReadOnlyCollection<Guid> restaurantIds,
        string status,
        CancellationToken ct,
        Guid? hubId = null,
        DateOnly? serviceDate = null) =>
        Task.FromResult<IReadOnlyList<OrderStatusLookupDto>>(
            _orders.Values
                .Where(o => o.Status == status
                            && restaurantIds.Contains(o.RestaurantId)
                            && (hubId == null || o.HubId == hubId))
                .ToList());

    public Task<IReadOnlyList<(Guid RestaurantId, int OrderCount)>> ListRoutableRestaurantsAsync(
        DateOnly serviceDate,
        IReadOnlyCollection<string> statuses,
        CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<(Guid RestaurantId, int OrderCount)>>([]);
}
