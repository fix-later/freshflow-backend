namespace FreshFlow.Logistics.Application.Abstractions;

public interface IOrderMarketReader
{
    public Task<IReadOnlyList<(Guid MarketId, string MarketName, int OrderCount)>>
        ListRoutableMarketsAsync(
            DateOnly serviceDate,
            IReadOnlyCollection<string> statuses,
            CancellationToken ct);
}
