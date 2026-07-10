using FreshFlow.Hub.Application.Abstractions;
using FreshFlow.Hub.Domain.Entities;

namespace FreshFlow.Hub.UnitTests.TestDoubles;

internal sealed class InMemoryHubDiscrepancyRepository : IHubDiscrepancyRepository, IHubDiscrepancyReader
{
    private readonly List<HubDiscrepancy> _discrepancies = [];

    public IReadOnlyList<HubDiscrepancy> Discrepancies => _discrepancies.AsReadOnly();
    public int SaveChangesCount { get; private set; }

    public Task AddAsync(HubDiscrepancy discrepancy, CancellationToken ct)
    {
        _discrepancies.Add(discrepancy);
        return Task.CompletedTask;
    }

    public Task<HubDiscrepancy?> FindByIdForHubAsync(Guid hubId, Guid discrepancyId, CancellationToken ct) =>
        Task.FromResult(_discrepancies.FirstOrDefault(d =>
            d.Id == discrepancyId &&
            d.HubId == hubId &&
            d.DeletedAt == null));

    public Task<(IReadOnlyList<HubDiscrepancy> Items, string? NextCursor)> GetPageAsync(
        Guid hubId,
        string? status,
        string? cursor,
        int pageSize,
        CancellationToken ct)
    {
        var items = _discrepancies
            .Where(d => d.HubId == hubId && d.DeletedAt == null)
            .Where(d => status is null || d.Status == status)
            .OrderByDescending(d => d.CreatedAt)
            .Take(pageSize)
            .ToList()
            .AsReadOnly();

        return Task.FromResult<(IReadOnlyList<HubDiscrepancy>, string?)>((items, null));
    }

    public Task<bool> HasOpenDiscrepanciesForOrderAsync(Guid orderId, CancellationToken ct) =>
        Task.FromResult(_discrepancies.Any(d =>
            d.OrderId == orderId &&
            d.Status == HubDiscrepancy.StatusOpen &&
            d.DeletedAt == null));

    public Task SaveChangesAsync(CancellationToken ct)
    {
        SaveChangesCount++;
        return Task.CompletedTask;
    }
}
