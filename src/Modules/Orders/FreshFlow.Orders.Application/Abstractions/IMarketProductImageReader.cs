namespace FreshFlow.Orders.Application.Abstractions;

public interface IMarketProductImageReader
{
    public Task<IReadOnlyDictionary<Guid, string>> ReadImagesAsync(
        IReadOnlyCollection<Guid> marketProductIds,
        CancellationToken ct);
}
