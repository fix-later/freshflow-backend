namespace FreshFlow.Hub.Application.Abstractions;

public interface IHubDiscrepancyReader
{
    public Task<bool> HasOpenDiscrepanciesForOrderAsync(Guid orderId, CancellationToken ct);
}
