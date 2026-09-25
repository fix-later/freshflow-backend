using FreshFlow.Analytics.Application.Abstractions;
using FreshFlow.Analytics.Application.Caching;
using FreshFlow.Analytics.Application.Common;
using FreshFlow.Analytics.Application.Dtos;
using FreshFlow.SharedKernel.Application;
using FreshFlow.SharedKernel.Caching;
using MediatR;

namespace FreshFlow.Analytics.Application.Queries.GetDemandTimeDistribution;

internal sealed class GetDemandTimeDistributionQueryHandler(IDemandHeatmapReader reader, AnalyticsCache cache)
    : IRequestHandler<GetDemandTimeDistributionQuery, Result<IReadOnlyList<TimeDistributionCellDto>>>
{
    public async Task<Result<IReadOnlyList<TimeDistributionCellDto>>> Handle(
        GetDemandTimeDistributionQuery request,
        CancellationToken ct)
    {
        var value = await cache.GetOrCreateAsync(
            RedisKeys.AnalyticsDemandTimeDistribution(request.From, request.To),
            request.To,
            BuildAsync,
            ct);

        return Result<IReadOnlyList<TimeDistributionCellDto>>.Success(value);

        async ValueTask<IReadOnlyList<TimeDistributionCellDto>> BuildAsync(CancellationToken factoryCt)
        {
            var (startUtc, endUtc) = VietnamTime.GetUtcBounds(request.From, request.To);
            var rows = await reader.ReadTimeDistributionAsync(startUtc, endUtc, factoryCt);
            var cells = rows
                .Select(row => new TimeDistributionCellDto(
                    row.DayOfWeek,
                    row.HourOfDay,
                    row.OrderCount))
                .ToArray();

            return cells;
        }
    }
}
