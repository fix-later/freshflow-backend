using FreshFlow.Analytics.Application.Abstractions;
using FreshFlow.Analytics.Application.Common;
using FreshFlow.Analytics.Application.Dtos;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Analytics.Application.Queries.GetDeliveryPerformance;

internal sealed class GetDeliveryPerformanceQueryHandler(IDeliveryPerformanceReader reader)
    : IRequestHandler<GetDeliveryPerformanceQuery, Result<DeliveryPerformanceDto>>
{
    public async Task<Result<DeliveryPerformanceDto>> Handle(
        GetDeliveryPerformanceQuery request,
        CancellationToken ct)
    {
        var (startUtc, endUtc) = VietnamTime.GetUtcBounds(request.From, request.To);
        var metrics = await reader.ReadAsync(startUtc, endUtc, ct);
        var judgedCount = metrics.OnTimeCount + metrics.LateCount;
        var utilizationPercentages = metrics.VehicleUtilizationPercentages
            .Select(percent => Math.Min(100m, percent))
            .ToArray();

        return Result<DeliveryPerformanceDto>.Success(new DeliveryPerformanceDto(
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
            utilizationPercentages.Length));
    }
}
