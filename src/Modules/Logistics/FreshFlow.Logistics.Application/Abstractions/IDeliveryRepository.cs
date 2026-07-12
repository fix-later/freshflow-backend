using FreshFlow.Logistics.Domain.Entities;

namespace FreshFlow.Logistics.Application.Abstractions;

public interface IDeliveryRepository
{
    public Task AddRangeAsync(IReadOnlyList<Delivery> deliveries, CancellationToken ct);

    public Task<bool> ExistsForOrderAsync(Guid orderId, CancellationToken ct);

    public Task<Delivery?> FindByIdAsync(Guid deliveryId, CancellationToken ct);

    public Task<IReadOnlyList<Delivery>> GetByRouteIdsAsync(
        IReadOnlyCollection<Guid> routeIds,
        CancellationToken ct);

    public Task SaveChangesAsync(CancellationToken ct);
}
