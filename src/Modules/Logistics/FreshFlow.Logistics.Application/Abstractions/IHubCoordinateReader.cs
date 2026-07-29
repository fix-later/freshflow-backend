namespace FreshFlow.Logistics.Application.Abstractions;

public interface IHubCoordinateReader
{
    public Task<HubCoordinateDto?> FindByIdAsync(Guid id, CancellationToken ct);
    public Task<HubCoordinateDto?> FindByMarketIdAsync(Guid marketId, CancellationToken ct);
}

public sealed record HubCoordinateDto(
    Guid Id,
    Guid? MarketId,
    string Name,
    decimal? Latitude,
    decimal? Longitude);
