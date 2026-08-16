namespace FreshFlow.Logistics.Application.Abstractions;

public interface IMarketSessionReader
{
    public Task<MarketSessionLookupDto?> FindByIdAsync(Guid sessionId, CancellationToken ct);
}

public sealed record MarketSessionLookupDto(
    Guid Id,
    Guid HubId,
    DateOnly ServiceDate,
    string Status);
