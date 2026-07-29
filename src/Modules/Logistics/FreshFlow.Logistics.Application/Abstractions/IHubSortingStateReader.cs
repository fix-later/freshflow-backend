namespace FreshFlow.Logistics.Application.Abstractions;

public interface IHubSortingStateReader
{
    public Task<bool> HasSortedLinesAsync(
        Guid routeId,
        Guid hubId,
        DateOnly serviceDate,
        CancellationToken ct);
}
