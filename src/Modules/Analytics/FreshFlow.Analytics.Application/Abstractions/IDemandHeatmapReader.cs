namespace FreshFlow.Analytics.Application.Abstractions;

public interface IDemandHeatmapReader
{
    public Task<IReadOnlyList<DemandHeatmapAggregateReadModel>> ReadHeatmapAsync(
        DateTime startUtcInclusive,
        DateTime endUtcExclusive,
        CancellationToken ct);

    public Task<IReadOnlyList<TimeDistributionCellReadModel>> ReadTimeDistributionAsync(
        DateTime startUtcInclusive,
        DateTime endUtcExclusive,
        CancellationToken ct);
}

public sealed record DemandHeatmapAggregateReadModel(
    Guid RestaurantId,
    string RestaurantName,
    decimal Latitude,
    decimal Longitude,
    string Status,
    int OrderCount,
    decimal TotalOrderValueVND,
    string? DominantProductCategory);

public sealed record TimeDistributionCellReadModel(
    int DayOfWeek,
    int HourOfDay,
    int OrderCount);
