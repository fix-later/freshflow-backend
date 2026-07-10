using FreshFlow.Hub.Application.Abstractions;
using FreshFlow.Hub.Domain.Entities;

namespace FreshFlow.Hub.UnitTests.TestDoubles;

internal sealed class InMemoryHubHandoverRepository : IHubHandoverRepository
{
    private readonly List<HubHandoverEvent> _handovers = [];

    public IReadOnlyList<HubHandoverEvent> Handovers => _handovers.AsReadOnly();
    public int SaveChangesCount { get; private set; }

    public Task AddAsync(HubHandoverEvent handover, CancellationToken ct)
    {
        _handovers.Add(handover);
        return Task.CompletedTask;
    }

    public Task<HubHandoverEvent?> FindByIdAsync(Guid hubId, Guid handoverId, CancellationToken ct) =>
        Task.FromResult(_handovers.FirstOrDefault(h =>
            h.Id == handoverId &&
            h.HubId == hubId &&
            h.DeletedAt == null));

    public Task<(IReadOnlyList<HubHandoverEvent> Items, string? NextCursor)> GetPageAsync(
        Guid hubId,
        string? cursor,
        int pageSize,
        CancellationToken ct) =>
        Task.FromResult<(IReadOnlyList<HubHandoverEvent>, string?)>(
            (_handovers.Where(h => h.HubId == hubId).Take(pageSize).ToList().AsReadOnly(), null));

    public Task SaveChangesAsync(CancellationToken ct)
    {
        SaveChangesCount++;
        return Task.CompletedTask;
    }
}
