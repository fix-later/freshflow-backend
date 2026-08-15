namespace FreshFlow.Procurement.Application.Abstractions;

public interface IMarketProductImageReader
{
    public Task<IReadOnlyDictionary<Guid, MarketProductInfoDto>> ReadInfoAsync(
        IReadOnlyCollection<Guid> marketProductIds,
        CancellationToken ct);
}

public sealed record MarketProductInfoDto(
    string? ImageUrl,
    string? PackingCode,
    decimal? PackingCapacityKg);
