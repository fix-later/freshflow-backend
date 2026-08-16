using FreshFlow.Hub.Application.Abstractions;

namespace FreshFlow.Hub.UnitTests.TestDoubles;

internal sealed class InMemoryOrderLookupReader : IOrderLookupReader
{
    private readonly Dictionary<Guid, OrderLookupDto> _orders = [];
    private readonly HashSet<(Guid ProcurementBatchId, Guid OrderId)> _batchOrders = [];

    public void Add(
        Guid orderItemId,
        Guid orderId,
        Guid marketProductId,
        decimal quantity = 10m,
        decimal? actualQuantity = null,
        Guid? procurementBatchId = null)
    {
        _orders[orderItemId] = new OrderLookupDto(orderItemId, orderId, marketProductId, quantity, actualQuantity);
        if (procurementBatchId is not null)
            _batchOrders.Add((procurementBatchId.Value, orderId));
    }

    public Task<OrderLookupDto?> FindByOrderItemIdAsync(Guid orderItemId, CancellationToken ct) =>
        Task.FromResult(_orders.GetValueOrDefault(orderItemId));

    public Task<bool> IsOrderInProcurementBatchAsync(
        Guid procurementBatchId,
        Guid orderId,
        CancellationToken ct) =>
        Task.FromResult(_batchOrders.Contains((procurementBatchId, orderId)));

    public Task<IReadOnlyList<OrderLookupDto>> FindByOrderItemIdsAsync(
        IReadOnlyCollection<Guid> orderItemIds,
        CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<OrderLookupDto>>(
            orderItemIds.Where(_orders.ContainsKey).Select(id => _orders[id]).ToList());
}
