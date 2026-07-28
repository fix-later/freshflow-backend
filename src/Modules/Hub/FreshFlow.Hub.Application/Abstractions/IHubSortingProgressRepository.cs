using FreshFlow.Hub.Domain.Entities;

namespace FreshFlow.Hub.Application.Abstractions;

public interface IHubSortingProgressRepository
{
    public Task<HubSortingProgress?> FindByRouteAndOrderItemAsync(
        Guid routeId, Guid orderItemId, CancellationToken ct);

    public Task<IReadOnlyList<HubSortingProgress>> ListByRouteAsync(Guid routeId, CancellationToken ct);

    public Task AddAsync(HubSortingProgress progress, CancellationToken ct);

    public Task SaveChangesAsync(CancellationToken ct);
}
