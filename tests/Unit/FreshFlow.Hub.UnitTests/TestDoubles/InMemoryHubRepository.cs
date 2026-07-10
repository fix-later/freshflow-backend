using FreshFlow.Hub.Application.Abstractions;
using HubEntity = FreshFlow.Hub.Domain.Entities.Hub;

namespace FreshFlow.Hub.UnitTests.TestDoubles;

internal sealed class InMemoryHubRepository : IHubRepository
{
    private readonly List<HubEntity> _hubs = [];

    public IReadOnlyList<HubEntity> Hubs => _hubs.AsReadOnly();
    public bool HasPendingInboundResult { get; set; }
    public int SaveChangesCount { get; private set; }

    public Task AddAsync(HubEntity hub, CancellationToken ct)
    {
        _hubs.Add(hub);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken ct)
    {
        SaveChangesCount++;
        return Task.CompletedTask;
    }

    public Task<HubEntity?> FindByIdAsync(Guid id, CancellationToken ct) =>
        Task.FromResult(_hubs.FirstOrDefault(h => h.Id == id));

    public Task<bool> HasPendingInboundAsync(Guid hubId, CancellationToken ct) =>
        Task.FromResult(HasPendingInboundResult);

    public Task<(IReadOnlyList<HubEntity> Items, string? NextCursor)> GetPageAsync(
        string? cursor,
        int pageSize,
        bool? isActive,
        CancellationToken ct)
    {
        var query = _hubs.AsEnumerable();
        if (isActive.HasValue)
            query = query.Where(h => h.IsActive == isActive.Value);

        return Task.FromResult<(IReadOnlyList<HubEntity>, string?)>(
            (query.Take(pageSize).ToList().AsReadOnly(), null));
    }
}
