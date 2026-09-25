using FreshFlow.Analytics.Application.Abstractions;
using FreshFlow.Analytics.Application.Caching;
using FreshFlow.Analytics.Application.Common;
using FreshFlow.Analytics.Application.Dtos;
using FreshFlow.SharedKernel.Application;
using FreshFlow.SharedKernel.Caching;
using MediatR;

namespace FreshFlow.Analytics.Application.Queries.GetDashboardOverview;

internal sealed class GetDashboardOverviewQueryHandler(
    IDashboardOverviewReader reader,
    AnalyticsCache cache)
    : IRequestHandler<GetDashboardOverviewQuery, Result<DashboardOverviewDto>>
{
    public async Task<Result<DashboardOverviewDto>> Handle(
        GetDashboardOverviewQuery request,
        CancellationToken ct)
    {
        // Resolve the business date before building the key. Keying an unresolved "today" would
        // go on serving yesterday's numbers past midnight in Vietnam until the TTL ran out.
        var date = request.Date ?? cache.Today;

        var value = await cache.GetOrCreateAsync(
            RedisKeys.AnalyticsOverview(date),
            date,
            BuildAsync,
            ct);

        return Result<DashboardOverviewDto>.Success(value);

        async ValueTask<DashboardOverviewDto> BuildAsync(CancellationToken factoryCt)
        {
            var (startUtc, endUtc) = VietnamTime.GetUtcBounds(date, date);
            var data = await reader.ReadAsync(date, startUtc, endUtc, factoryCt);
            var onTimeRate = data.DeliveriesToday == 0
                ? 0m
                : Math.Round(data.OnTimeDeliveriesToday * 100m / data.DeliveriesToday, 2);

            return new DashboardOverviewDto(
                data.OrdersToday,
                data.RevenueToday,
                data.PendingOrders,
                data.CancelledToday,
                data.ActiveProcurementBatches,
                data.DeliveriesToday,
                onTimeRate,
                data.HubInboundKgToday,
                data.HubOutboundKgToday);
        }
    }
}
