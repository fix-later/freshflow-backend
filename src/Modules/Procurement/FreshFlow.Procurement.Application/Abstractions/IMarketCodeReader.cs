namespace FreshFlow.Procurement.Application.Abstractions;

public interface IMarketCodeReader
{
    public Task<IReadOnlyList<Guid>> ListActiveMarketIdsAsync(CancellationToken ct);

    public Task<IReadOnlyDictionary<Guid, (string? Code, string Name)>> ReadMarketCodesAsync(
        IReadOnlyCollection<Guid> marketIds,
        CancellationToken ct);
}
