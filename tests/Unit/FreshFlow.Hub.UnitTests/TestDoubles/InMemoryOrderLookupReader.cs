using FreshFlow.Hub.Application.Abstractions;

namespace FreshFlow.Hub.UnitTests.TestDoubles;

internal sealed class InMemoryOrderLookupReader : IOrderLookupReader
{
    private readonly Dictionary<Guid, OrderLookupDto> _orders = [];

    public void Add(
        Guid orderItemId,
        Guid orderId,
        Guid marketProductId,
        decimal quantity = 10m,
        decimal? actualQuantity = null) =>
        _orders[orderItemId] = new OrderLookupDto(orderItemId, orderId, marketProductId, quantity, actualQuantity);

    public Task<OrderLookupDto?> FindByOrderItemIdAsync(Guid orderItemId, CancellationToken ct) =>
        Task.FromResult(_orders.GetValueOrDefault(orderItemId));

    public Task<IReadOnlyList<OrderLookupDto>> FindByOrderItemIdsAsync(
        IReadOnlyCollection<Guid> orderItemIds,
        CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<OrderLookupDto>>(
            orderItemIds.Where(_orders.ContainsKey).Select(id => _orders[id]).ToList());
}
