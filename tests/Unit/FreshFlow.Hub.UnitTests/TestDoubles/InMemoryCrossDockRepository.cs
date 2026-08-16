using FreshFlow.Hub.Application.Abstractions;
using FreshFlow.Hub.Domain.Entities;

namespace FreshFlow.Hub.UnitTests.TestDoubles;

internal sealed class InMemoryCrossDockRepository : ICrossDockRepository
{
    private readonly List<CrossDockTransfer> _transfers = [];

    public IReadOnlyList<CrossDockTransfer> Transfers => _transfers.AsReadOnly();
    public int SaveChangesCount { get; private set; }

    public Task AddAsync(CrossDockTransfer transfer, CancellationToken ct)
    {
        _transfers.Add(transfer);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken ct)
    {
        SaveChangesCount++;
        return Task.CompletedTask;
    }

    public Task<(IReadOnlyList<CrossDockTransfer> Items, string? NextCursor)> GetPageAsync(
        Guid hubId,
        string? status,
        string? cursor,
        int pageSize,
        CancellationToken ct)
    {
        var query = _transfers.Where(t => t.HubId == hubId && t.DeletedAt == null);
        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(t => t.Status == status);

        var items = query
            .OrderByDescending(t => t.CreatedAt)
            .Take(pageSize)
            .ToList()
            .AsReadOnly();

        return Task.FromResult<(IReadOnlyList<CrossDockTransfer>, string?)>((items, null));
    }
}
