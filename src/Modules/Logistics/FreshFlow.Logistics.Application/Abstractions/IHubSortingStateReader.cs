namespace FreshFlow.Logistics.Application.Abstractions;

public interface IHubSortingStateReader
{
    public Task<bool> HasSortedLinesAsync(
        Guid routeId,
        Guid marketId,
        DateOnly serviceDate,
        CancellationToken ct);
}
