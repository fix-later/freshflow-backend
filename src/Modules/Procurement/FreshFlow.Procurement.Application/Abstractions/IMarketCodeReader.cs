namespace FreshFlow.Procurement.Application.Abstractions;

public interface IMarketCodeReader
{
    public Task<IReadOnlyDictionary<Guid, (string? Code, string Name)>> ReadMarketCodesAsync(
        IReadOnlyCollection<Guid> marketIds,
        CancellationToken ct);
}
