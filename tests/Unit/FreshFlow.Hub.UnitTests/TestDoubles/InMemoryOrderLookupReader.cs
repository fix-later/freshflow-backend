using FreshFlow.Hub.Application.Abstractions;

namespace FreshFlow.Hub.UnitTests.TestDoubles;

internal sealed class InMemoryOrderLookupReader : IOrderLookupReader
{
    private readonly Dictionary<Guid, OrderLookupDto> _orders = [];

    public void Add(Guid orderItemId, Guid orderId) =>
        _orders[orderItemId] = new OrderLookupDto(orderItemId, orderId);

    public Task<OrderLookupDto?> FindByOrderItemIdAsync(Guid orderItemId, CancellationToken ct) =>
        Task.FromResult(_orders.GetValueOrDefault(orderItemId));
}
