namespace FreshFlow.Analytics.Application.Dtos;

public sealed record DemandHeatmapPointDto(
    Guid RestaurantId,
    string RestaurantName,
    decimal Latitude,
    decimal Longitude,
    int TotalOrderCount,
    decimal TotalOrderValueVND,
    string? DominantProductCategory);

public sealed record TimeDistributionCellDto(
    int DayOfWeek,
    int HourOfDay,
    int OrderCount);
