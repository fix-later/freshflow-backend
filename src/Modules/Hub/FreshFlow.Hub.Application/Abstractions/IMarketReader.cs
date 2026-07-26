namespace FreshFlow.Hub.Application.Abstractions;

public sealed record MarketSnapshot(Guid Id, bool IsActive);

public interface IMarketReader
{
    public Task<MarketSnapshot?> FindAsync(Guid marketId, CancellationToken ct);
}
