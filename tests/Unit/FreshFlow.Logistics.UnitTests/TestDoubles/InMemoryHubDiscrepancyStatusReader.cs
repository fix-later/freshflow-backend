using FreshFlow.Logistics.Application.Abstractions;

namespace FreshFlow.Logistics.UnitTests.TestDoubles;

internal sealed class InMemoryHubDiscrepancyStatusReader : IHubDiscrepancyStatusReader
{
    private readonly HashSet<Guid> _openOrderIds = [];

    public IReadOnlyCollection<Guid> LastOrderIds { get; private set; } = [];

    public void AddOpenDiscrepancy(Guid orderId) => _openOrderIds.Add(orderId);

    public Task<IReadOnlyList<Guid>> GetOrdersWithOpenDiscrepanciesAsync(
        IReadOnlyCollection<Guid> orderIds,
        CancellationToken ct)
    {
        LastOrderIds = orderIds.ToArray();
        return Task.FromResult<IReadOnlyList<Guid>>(
            orderIds.Where(_openOrderIds.Contains).ToList().AsReadOnly());
    }
}
