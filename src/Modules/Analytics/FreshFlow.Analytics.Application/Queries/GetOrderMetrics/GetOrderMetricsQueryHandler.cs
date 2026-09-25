using FreshFlow.Analytics.Application.Abstractions;
using FreshFlow.Analytics.Application.Caching;
using FreshFlow.Analytics.Application.Common;
using FreshFlow.Analytics.Application.Dtos;
using FreshFlow.SharedKernel.Application;
using FreshFlow.SharedKernel.Caching;
using MediatR;

namespace FreshFlow.Analytics.Application.Queries.GetOrderMetrics;

internal sealed class GetOrderMetricsQueryHandler(IOrderMetricsReader reader, AnalyticsCache cache)
    : IRequestHandler<GetOrderMetricsQuery, Result<OrderMetricsDto>>
{
    private const string DraftStatus = "Draft";
    private const string DeliveredStatus = "Delivered";
    private const string CancelledStatus = "Cancelled";
    private static readonly string[] OrderStatuses =
    [
        DraftStatus,
        "Confirmed",
        "Batched",
        "PickedUp",
        "AtHub",
        "Delivering",
        DeliveredStatus,
        CancelledStatus
    ];

    public async Task<Result<OrderMetricsDto>> Handle(
        GetOrderMetricsQuery request,
        CancellationToken ct)
    {
        var value = await cache.GetOrCreateAsync(
            RedisKeys.AnalyticsOrderMetrics(request.From, request.To, request.RestaurantId, request.GroupBy),
            request.To,
            BuildAsync,
            ct);

        return Result<OrderMetricsDto>.Success(value);

        async ValueTask<OrderMetricsDto> BuildAsync(CancellationToken factoryCt)
        {
            var groupBy = request.GroupBy?.ToLowerInvariant() ?? "day";
            var (startUtc, endUtc) = VietnamTime.GetUtcBounds(request.From, request.To);
            var rows = await reader.ReadAsync(
                startUtc,
                endUtc,
                request.RestaurantId,
                groupBy,
                factoryCt);
            var observedStatusCounts = rows
                .GroupBy(row => row.Status)
                .ToDictionary(group => group.Key, group => group.Sum(row => row.OrderCount));
            var statusCounts = OrderStatuses.ToDictionary(
                status => status,
                status => observedStatusCounts.GetValueOrDefault(status));
            var totalOrders = rows.Sum(row => row.OrderCount);
            var totalRevenue = Revenue(rows);
            var cancelledCount = statusCounts[CancelledStatus];
            var buckets = rows
                .GroupBy(row => row.Date)
                .OrderBy(group => group.Key)
                .Select(group => new OrderMetricsBucketDto(
                    group.Key,
                    group.Sum(row => row.OrderCount),
                    Revenue(group)))
                .ToArray();

            return new OrderMetricsDto(
                new OrderMetricsSummaryDto(
                    totalOrders,
                    totalRevenue,
                    totalOrders == 0 ? 0m : totalRevenue / totalOrders,
                    cancelledCount,
                    totalOrders == 0 ? 0m : Math.Round(cancelledCount * 100m / totalOrders, 2),
                    statusCounts[DeliveredStatus],
                    statusCounts),
                buckets);
        }
    }

    private static decimal Revenue(IEnumerable<OrderMetricsBucketReadModel> rows) =>
        rows.Where(row => row.Status != CancelledStatus && row.Status != DraftStatus)
            .Sum(row => row.TotalAmountVND);
}
