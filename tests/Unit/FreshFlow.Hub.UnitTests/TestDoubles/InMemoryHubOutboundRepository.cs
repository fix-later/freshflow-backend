using FreshFlow.Hub.Application.Abstractions;
using FreshFlow.Hub.Domain.Entities;

namespace FreshFlow.Hub.UnitTests.TestDoubles;

internal sealed class InMemoryHubOutboundRepository : IHubOutboundRepository
{
    private readonly List<HubOutboundEvent> _outbounds = [];

    public IReadOnlyList<HubOutboundEvent> Outbounds => _outbounds.AsReadOnly();
    public int SaveChangesCount { get; private set; }

    public Task AddAsync(HubOutboundEvent outbound, CancellationToken ct)
    {
        _outbounds.Add(outbound);
        return Task.CompletedTask;
    }

    public Task<HubOutboundEvent?> FindByIdForHubAsync(Guid hubId, Guid outboundId, CancellationToken ct) =>
        Task.FromResult(_outbounds.FirstOrDefault(e =>
            e.Id == outboundId &&
            e.HubId == hubId &&
            e.DeletedAt == null));

    public Task SaveChangesAsync(CancellationToken ct)
    {
        SaveChangesCount++;
        return Task.CompletedTask;
    }

    public Task<(IReadOnlyList<HubOutboundEvent> Items, string? NextCursor, decimal TotalQuantityKg)> GetHistoryPageAsync(
        Guid hubId,
        DateOnly? date,
        string? cursor,
        int pageSize,
        CancellationToken ct)
    {
        var query = _outbounds.Where(e => e.HubId == hubId && e.DeletedAt == null);
        if (date.HasValue)
        {
            var start = date.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            var end = start.AddDays(1);
            query = query.Where(e => e.DispatchedAt >= start && e.DispatchedAt < end);
        }

        var filtered = query.ToList();
        var items = filtered
            .OrderByDescending(e => e.CreatedAt)
            .Take(pageSize)
            .ToList()
            .AsReadOnly();

        return Task.FromResult<(IReadOnlyList<HubOutboundEvent>, string?, decimal)>(
            (items, null, filtered.Sum(e => e.TotalQuantityKg)));
    }
}
