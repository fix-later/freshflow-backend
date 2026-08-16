using FreshFlow.Hub.Application.Abstractions;
using FreshFlow.Hub.Domain.Entities;

namespace FreshFlow.Hub.UnitTests.TestDoubles;

internal sealed class InMemoryHubSortingProgressRepository : IHubSortingProgressRepository
{
    private readonly List<HubSortingProgress> _lines = [];

    public IReadOnlyList<HubSortingProgress> Lines => _lines.AsReadOnly();
    public int SaveChangesCount { get; private set; }

    public Task<HubSortingProgress?> FindByHubDateAndOrderItemAsync(
        Guid hubId, DateOnly serviceDate, Guid orderItemId, CancellationToken ct) =>
        Task.FromResult(_lines.FirstOrDefault(l =>
            l.HubId == hubId &&
            l.ServiceDate == serviceDate &&
            l.OrderItemId == orderItemId &&
            l.DeletedAt == null));

    public Task<IReadOnlyList<HubSortingProgress>> ListByHubAndDateAsync(
        Guid hubId, DateOnly serviceDate, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<HubSortingProgress>>(
            _lines.Where(l => l.HubId == hubId && l.ServiceDate == serviceDate && l.DeletedAt == null)
                .ToList().AsReadOnly());

    public Task AddAsync(HubSortingProgress progress, CancellationToken ct)
    {
        _lines.Add(progress);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken ct)
    {
        SaveChangesCount++;
        return Task.CompletedTask;
    }
}
