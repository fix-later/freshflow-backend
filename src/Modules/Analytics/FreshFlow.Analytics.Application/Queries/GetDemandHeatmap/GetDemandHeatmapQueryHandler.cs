using FreshFlow.Analytics.Application.Abstractions;
using FreshFlow.Analytics.Application.Caching;
using FreshFlow.Analytics.Application.Common;
using FreshFlow.Analytics.Application.Dtos;
using FreshFlow.SharedKernel.Application;
using FreshFlow.SharedKernel.Caching;
using MediatR;

namespace FreshFlow.Analytics.Application.Queries.GetDemandHeatmap;

internal sealed class GetDemandHeatmapQueryHandler(IDemandHeatmapReader reader, AnalyticsCache cache)
    : IRequestHandler<GetDemandHeatmapQuery, Result<IReadOnlyList<DemandHeatmapPointDto>>>
{
    private const string DraftStatus = "Draft";
    private const string CancelledStatus = "Cancelled";

    public async Task<Result<IReadOnlyList<DemandHeatmapPointDto>>> Handle(
        GetDemandHeatmapQuery request,
        CancellationToken ct)
    {
        var value = await cache.GetOrCreateAsync(
            RedisKeys.AnalyticsDemandHeatmap(request.From, request.To),
            request.To,
            BuildAsync,
            ct);

        return Result<IReadOnlyList<DemandHeatmapPointDto>>.Success(value);

        async ValueTask<IReadOnlyList<DemandHeatmapPointDto>> BuildAsync(CancellationToken factoryCt)
        {
            var (startUtc, endUtc) = VietnamTime.GetUtcBounds(request.From, request.To);
            var rows = await reader.ReadHeatmapAsync(startUtc, endUtc, factoryCt);
            var points = rows
                .GroupBy(row => new
                {
                    row.RestaurantId,
                    row.RestaurantName,
                    row.Latitude,
                    row.Longitude,
                    row.DominantProductCategory
                })
                .OrderBy(group => group.Key.RestaurantName)
                .ThenBy(group => group.Key.RestaurantId)
                .Select(group => new DemandHeatmapPointDto(
                    group.Key.RestaurantId,
                    group.Key.RestaurantName,
                    group.Key.Latitude,
                    group.Key.Longitude,
                    group.Sum(row => row.OrderCount),
                    group.Where(row => row.Status != CancelledStatus && row.Status != DraftStatus)
                        .Sum(row => row.TotalOrderValueVND),
                    group.Key.DominantProductCategory))
                .ToArray();

            return points;
        }
    }
}
