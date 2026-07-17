using FreshFlow.Analytics.Application.Abstractions;
using FreshFlow.Analytics.Application.Dtos;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Analytics.Application.Queries.GetProcurementMetrics;

internal sealed class GetProcurementMetricsQueryHandler(IProcurementMetricsReader reader)
    : IRequestHandler<GetProcurementMetricsQuery, Result<ProcurementMetricsDto>>
{
    private const string HandedOffStatus = "HandedOff";
    private static readonly string[] BatchStatuses =
    [
        "Built",
        "Manifested",
        "Purchasing",
        HandedOffStatus
    ];
    private static readonly string[] ExceptionTypes =
    [
        "Unavailable",
        "Shortfall",
        "PriceDiscrepancy",
        "Damaged"
    ];

    public async Task<Result<ProcurementMetricsDto>> Handle(
        GetProcurementMetricsQuery request,
        CancellationToken ct)
    {
        var metrics = await reader.ReadAsync(request.From, request.To, request.MarketId, ct);
        var observedStatusCounts = metrics.StatusCounts.ToDictionary(row => row.Name, row => row.Count);
        var statusCounts = BatchStatuses.ToDictionary(
            status => status,
            status => observedStatusCounts.GetValueOrDefault(status));
        var observedExceptionCounts = metrics.ExceptionsByType.ToDictionary(
            row => row.Name,
            row => row.Count);
        var exceptionsByType = ExceptionTypes.ToDictionary(
            type => type,
            type => observedExceptionCounts.GetValueOrDefault(type));
        var totalBatches = metrics.StatusCounts.Sum(row => row.Count);

        return Result<ProcurementMetricsDto>.Success(new ProcurementMetricsDto(
            totalBatches,
            statusCounts,
            totalBatches == 0
                ? 0m
                : Math.Round(statusCounts[HandedOffStatus] * 100m / totalBatches, 2),
            metrics.ItemsTotal,
            metrics.ItemsPurchased,
            metrics.ItemsTotal - metrics.ItemsPurchased,
            metrics.TotalActualCostVND,
            metrics.PriceVariancePercent is null
                ? null
                : Math.Round(metrics.PriceVariancePercent.Value, 2),
            metrics.AvgLeadTimeMinutes is null
                ? null
                : Math.Round(metrics.AvgLeadTimeMinutes.Value, 2),
            metrics.ExceptionsByType.Sum(row => row.Count),
            exceptionsByType));
    }
}
