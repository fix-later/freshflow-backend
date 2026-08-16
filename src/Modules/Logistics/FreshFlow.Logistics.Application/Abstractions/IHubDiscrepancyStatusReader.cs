namespace FreshFlow.Logistics.Application.Abstractions;

public interface IHubDiscrepancyStatusReader
{
    public Task<IReadOnlyList<Guid>> GetOrdersWithOpenDiscrepanciesAsync(
        IReadOnlyCollection<Guid> orderIds,
        CancellationToken ct);
}
