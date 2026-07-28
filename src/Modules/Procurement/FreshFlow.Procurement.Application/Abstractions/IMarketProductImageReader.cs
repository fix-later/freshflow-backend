namespace FreshFlow.Procurement.Application.Abstractions;

public interface IMarketProductImageReader
{
    /// <summary>
    /// Resolves market-product ids to their product image URL. Ids with no image are omitted.
    /// </summary>
    public Task<IReadOnlyDictionary<Guid, string>> ReadImagesAsync(
        IReadOnlyCollection<Guid> marketProductIds,
        CancellationToken ct);
}
