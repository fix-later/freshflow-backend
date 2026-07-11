using HubEntity = FreshFlow.Hub.Domain.Entities.Hub;

namespace FreshFlow.Hub.Application.Abstractions;

public interface IHubRepository
{
    public Task AddAsync(HubEntity hub, CancellationToken ct);
    public Task SaveChangesAsync(CancellationToken ct);
    public Task<HubEntity?> FindByIdAsync(Guid id, CancellationToken ct);
    public Task<bool> HasPendingInboundAsync(Guid hubId, CancellationToken ct);
    public Task<(IReadOnlyList<HubEntity> Items, string? NextCursor)> GetPageAsync(
        string? cursor,
        int pageSize,
        bool? isActive,
        CancellationToken ct);
}
