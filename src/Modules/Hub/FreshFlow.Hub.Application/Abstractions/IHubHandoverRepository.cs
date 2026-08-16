using FreshFlow.Hub.Domain.Entities;

namespace FreshFlow.Hub.Application.Abstractions;

public interface IHubHandoverRepository
{
    public Task AddAsync(HubHandoverEvent handover, CancellationToken ct);
    public Task<HubHandoverEvent?> FindByIdAsync(Guid hubId, Guid handoverId, CancellationToken ct);
    public Task<(IReadOnlyList<HubHandoverEvent> Items, string? NextCursor)> GetPageAsync(
        Guid hubId,
        string? cursor,
        int pageSize,
        CancellationToken ct);

    public Task SaveChangesAsync(CancellationToken ct);
}
