using FreshFlow.Logistics.Application.Abstractions;
using FreshFlow.Logistics.Domain.Entities;

namespace FreshFlow.Logistics.UnitTests.TestDoubles;

internal sealed class InMemoryDeliveryRepository : IDeliveryRepository
{
    private readonly List<Delivery> _deliveries = [];

    public IReadOnlyList<Delivery> Deliveries => _deliveries.AsReadOnly();
    public int SaveChangesCount { get; private set; }
    public int GetByRouteIdsCount { get; private set; }
    public IReadOnlyCollection<Guid> LastRouteIds { get; private set; } = [];

    public Task AddRangeAsync(IReadOnlyList<Delivery> deliveries, CancellationToken ct)
    {
        _deliveries.AddRange(deliveries);
        return Task.CompletedTask;
    }

    public Task<bool> ExistsForOrderAsync(Guid orderId, CancellationToken ct) =>
        Task.FromResult(_deliveries.Any(d => d.OrderId == orderId));

    public Task<IReadOnlyList<Delivery>> GetByRouteIdsAsync(
        IReadOnlyCollection<Guid> routeIds,
        CancellationToken ct)
    {
        GetByRouteIdsCount++;
        LastRouteIds = routeIds.ToArray();
        return Task.FromResult<IReadOnlyList<Delivery>>(
            _deliveries
                .Where(delivery => routeIds.Contains(delivery.DeliveryRouteId))
                .OrderBy(delivery => delivery.SequenceNumber)
                .ToList()
                .AsReadOnly());
    }

    public Task SaveChangesAsync(CancellationToken ct)
    {
        SaveChangesCount++;
        return Task.CompletedTask;
    }
}
