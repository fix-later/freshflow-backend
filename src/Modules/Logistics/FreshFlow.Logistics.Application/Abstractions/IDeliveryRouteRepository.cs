using FreshFlow.Logistics.Domain.Entities;
using FreshFlow.Logistics.Domain.Enums;

namespace FreshFlow.Logistics.Application.Abstractions;

public interface IDeliveryRouteRepository
{
    public Task<DeliveryRoute?> FindByIdAsync(Guid id, CancellationToken ct);

    public Task<(IReadOnlyList<DeliveryRoute> Items, string? NextCursor)> GetPageAsync(
        string? cursor,
        int pageSize,
        DateOnly? serviceDate,
        RouteStatus? status,
        CancellationToken ct);

    public Task AddAsync(DeliveryRoute route, CancellationToken ct);

    public Task SaveChangesAsync(CancellationToken ct);
}
