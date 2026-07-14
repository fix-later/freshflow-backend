namespace FreshFlow.Procurement.Application.Abstractions;

public interface IMarketProductMarketReader
{
    public Task<IReadOnlyDictionary<Guid, Guid>> ReadMarketsAsync(
        IReadOnlyCollection<Guid> marketProductIds,
        CancellationToken ct);

    public Task<IReadOnlyDictionary<Guid, decimal>> ReadReferencePricesAsync(
        IReadOnlyCollection<Guid> marketProductIds,
        CancellationToken ct);
}
