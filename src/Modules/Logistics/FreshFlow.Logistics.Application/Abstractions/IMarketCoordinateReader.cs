namespace FreshFlow.Logistics.Application.Abstractions;

public interface IMarketCoordinateReader
{
    public Task<MarketCoordinateDto?> FindByIdAsync(Guid marketId, CancellationToken ct);
}

public sealed record MarketCoordinateDto(Guid Id, string Name, decimal? Latitude, decimal? Longitude);
