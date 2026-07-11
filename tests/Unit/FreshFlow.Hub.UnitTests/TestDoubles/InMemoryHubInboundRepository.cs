using FreshFlow.Hub.Application.Abstractions;
using FreshFlow.Hub.Domain.Entities;

namespace FreshFlow.Hub.UnitTests.TestDoubles;

internal sealed class InMemoryHubInboundRepository : IHubInboundRepository
{
    private readonly List<HubInboundEvent> _inbounds = [];

    public IReadOnlyList<HubInboundEvent> Inbounds => _inbounds.AsReadOnly();
    public bool ThrowConcurrencyOnSave { get; set; }
    public int SaveChangesCount { get; private set; }

    public Task AddAsync(HubInboundEvent inbound, CancellationToken ct)
    {
        _inbounds.Add(inbound);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken ct)
    {
        if (ThrowConcurrencyOnSave)
            throw new HubConcurrencyException("test", new Exception());

        SaveChangesCount++;
        return Task.CompletedTask;
    }

    public Task<HubInboundEvent?> FindPendingByIdAsync(Guid id, CancellationToken ct) =>
        Task.FromResult(_inbounds.FirstOrDefault(e =>
            e.Id == id &&
            e.Status == HubInboundEvent.StatusPending &&
            e.DeletedAt == null));

    public Task<HubInboundEvent?> FindByIdForHubAsync(Guid hubId, Guid inboundId, CancellationToken ct) =>
        Task.FromResult(_inbounds.FirstOrDefault(e =>
            e.Id == inboundId &&
            e.HubId == hubId &&
            e.DeletedAt == null));

    public Task<bool> ExistsForHubAsync(Guid hubId, Guid inboundId, CancellationToken ct) =>
        Task.FromResult(_inbounds.Any(e =>
            e.Id == inboundId &&
            e.HubId == hubId &&
            e.DeletedAt == null));

    public Task<bool> DeliveryScheduleExistsAsync(Guid hubId, Guid deliveryScheduleId, CancellationToken ct) =>
        Task.FromResult(_inbounds.Any(e =>
            e.HubId == hubId &&
            e.DeliveryScheduleId == deliveryScheduleId &&
            e.DeletedAt == null));

    public Task<(IReadOnlyList<HubInboundEvent> Items, string? NextCursor)> GetPendingPageAsync(
        Guid hubId,
        string? cursor,
        int pageSize,
        CancellationToken ct)
    {
        var items = _inbounds
            .Where(e =>
                e.HubId == hubId &&
                (e.Status == HubInboundEvent.StatusPending ||
                    e.Status == HubInboundEvent.StatusArrivedAtHub) &&
                e.DeletedAt == null)
            .OrderByDescending(e => e.CreatedAt)
            .Take(pageSize)
            .ToList()
            .AsReadOnly();

        return Task.FromResult<(IReadOnlyList<HubInboundEvent>, string?)>((items, null));
    }

    public Task<(IReadOnlyList<HubInboundEvent> Items, string? NextCursor, decimal TotalQuantityKg)> GetHistoryPageAsync(
        Guid hubId,
        DateOnly? date,
        string? cursor,
        int pageSize,
        CancellationToken ct)
    {
        var query = _inbounds.Where(e => e.HubId == hubId && e.DeletedAt == null);

        if (date.HasValue)
        {
            var start = date.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            var end = start.AddDays(1);
            query = query.Where(e => e.ArrivedAt >= start && e.ArrivedAt < end);
        }

        var filtered = query.ToList();
        var items = filtered
            .OrderByDescending(e => e.CreatedAt)
            .Take(pageSize)
            .ToList()
            .AsReadOnly();

        return Task.FromResult<(IReadOnlyList<HubInboundEvent>, string?, decimal)>(
            (items, null, filtered.Sum(e => e.TotalQuantityKg)));
    }
}
