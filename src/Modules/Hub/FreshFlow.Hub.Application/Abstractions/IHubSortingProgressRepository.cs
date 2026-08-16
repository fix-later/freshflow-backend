using FreshFlow.Hub.Domain.Entities;

namespace FreshFlow.Hub.Application.Abstractions;

public interface IHubSortingProgressRepository
{
    public Task<HubSortingProgress?> FindByHubDateAndOrderItemAsync(
        Guid hubId, DateOnly serviceDate, Guid orderItemId, CancellationToken ct);

    public Task<IReadOnlyList<HubSortingProgress>> ListByHubAndDateAsync(
        Guid hubId,
        DateOnly serviceDate,
        CancellationToken ct);

    public Task AddAsync(HubSortingProgress progress, CancellationToken ct);

    public Task SaveChangesAsync(CancellationToken ct);
}
