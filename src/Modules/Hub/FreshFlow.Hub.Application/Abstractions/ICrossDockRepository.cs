using FreshFlow.Hub.Domain.Entities;

namespace FreshFlow.Hub.Application.Abstractions;

public interface ICrossDockRepository
{
    public Task AddAsync(CrossDockTransfer transfer, CancellationToken ct);
    public Task SaveChangesAsync(CancellationToken ct);
    public Task<(IReadOnlyList<CrossDockTransfer> Items, string? NextCursor)> GetPageAsync(
        Guid hubId,
        string? status,
        string? cursor,
        int pageSize,
        CancellationToken ct);
}
