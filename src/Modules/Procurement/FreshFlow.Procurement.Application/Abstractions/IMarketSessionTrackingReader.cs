using FreshFlow.Procurement.Application.Dtos;

namespace FreshFlow.Procurement.Application.Abstractions;

public interface IMarketSessionTrackingReader
{
    public Task<MarketSessionTrackingData> ReadAsync(
        Guid marketSessionId, int page, int pageSize, CancellationToken ct);
}
