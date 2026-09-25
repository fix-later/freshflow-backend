using FreshFlow.Analytics.Application.Abstractions;
using FreshFlow.Analytics.Application.Caching;
using FreshFlow.Analytics.Application.Common;
using FreshFlow.Analytics.Application.Dtos;
using FreshFlow.SharedKernel.Application;
using FreshFlow.SharedKernel.Caching;
using MediatR;

namespace FreshFlow.Analytics.Application.Queries.GetDeliveryPerformance;

internal sealed class GetDeliveryPerformanceQueryHandler(IDeliveryPerformanceReader reader, AnalyticsCache cache)
    : IRequestHandler<GetDeliveryPerformanceQuery, Result<DeliveryPerformanceDto>>
{
    public async Task<Result<DeliveryPerformanceDto>> Handle(
        GetDeliveryPerformanceQuery request,
        CancellationToken ct)
    {
        var value = await cache.GetOrCreateAsync(
            RedisKeys.AnalyticsDeliveryPerformance(request.From, request.To),
            request.To,
            BuildAsync,
            ct);

        return Result<DeliveryPerformanceDto>.Success(value);

        async ValueTask<DeliveryPerformanceDto> BuildAsync(CancellationToken factoryCt)
        {
            var (startUtc, endUtc) = VietnamTime.GetUtcBounds(request.From, request.To);
            var metrics = await reader.ReadAsync(startUtc, endUtc, factoryCt);
            var judgedCount = metrics.OnTimeCount + metrics.LateCount;
            var utilizationPercentages = metrics.VehicleUtilizationPercentages
                .Select(percent => Math.Min(100m, percent))
                .ToArray();

            return new DeliveryPerformanceDto(
                metrics.TotalDeliveries,
                metrics.OnTimeCount,
                metrics.LateCount,
                judgedCount == 0
                    ? 0m
                    : Math.Round(metrics.OnTimeCount * 100m / judgedCount, 2),
                metrics.FailedCount,
                metrics.AvgDeliveryDurationMinutes,
                metrics.DurationSampleCount,
                utilizationPercentages.Length == 0
                    ? null
                    : utilizationPercentages.Average(),
                utilizationPercentages.Length);
        }
    }
}
