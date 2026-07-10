using FreshFlow.Hub.Domain.Entities;

namespace FreshFlow.Hub.Application.Abstractions;

public interface IHubDiscrepancyRepository
{
    public Task AddAsync(HubDiscrepancy discrepancy, CancellationToken ct);
    public Task<HubDiscrepancy?> FindByIdForHubAsync(Guid hubId, Guid discrepancyId, CancellationToken ct);

    public Task<(IReadOnlyList<HubDiscrepancy> Items, string? NextCursor)> GetPageAsync(
        Guid hubId,
        string? status,
        string? cursor,
        int pageSize,
        CancellationToken ct);

    public Task SaveChangesAsync(CancellationToken ct);
}
