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
    public bool TrySaveChangesResult { get; set; } = true;

    public Task AddRangeAsync(IReadOnlyList<Delivery> deliveries, CancellationToken ct)
    {
        _deliveries.AddRange(deliveries);
        return Task.CompletedTask;
    }

    public Task<bool> ExistsForOrderAsync(Guid orderId, CancellationToken ct) =>
        Task.FromResult(_deliveries.Any(d => d.OrderId == orderId));

    public Task<IReadOnlySet<Guid>> GetExistingOrderIdsAsync(
        IReadOnlyCollection<Guid> orderIds,
        CancellationToken ct) =>
        Task.FromResult<IReadOnlySet<Guid>>(_deliveries
            .Where(d => orderIds.Contains(d.OrderId))
            .Select(d => d.OrderId)
            .ToHashSet());

    public Task<Delivery?> FindByIdAsync(Guid deliveryId, CancellationToken ct) =>
        Task.FromResult(_deliveries.FirstOrDefault(d => d.Id == deliveryId));

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

    public Task<bool> TrySaveChangesAsync(CancellationToken ct)
    {
        SaveChangesCount++;
        return Task.FromResult(TrySaveChangesResult);
    }
}
