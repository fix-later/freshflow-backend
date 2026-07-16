using FreshFlow.Analytics.Application.Abstractions;
using FreshFlow.Analytics.Application.Common;
using FreshFlow.Analytics.Application.Dtos;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Analytics.Application.Queries.GetDashboardOverview;

internal sealed class GetDashboardOverviewQueryHandler(
    IDashboardOverviewReader reader,
    TimeProvider timeProvider)
    : IRequestHandler<GetDashboardOverviewQuery, Result<DashboardOverviewDto>>
{
    public async Task<Result<DashboardOverviewDto>> Handle(
        GetDashboardOverviewQuery request,
        CancellationToken ct)
    {
        var date = request.Date ?? DateOnly.FromDateTime(
            TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), VietnamTime.Zone).DateTime);
        var (startUtc, endUtc) = VietnamTime.GetUtcBounds(date, date);
        var data = await reader.ReadAsync(date, startUtc, endUtc, ct);
        var onTimeRate = data.DeliveriesToday == 0
            ? 0m
            : Math.Round(data.OnTimeDeliveriesToday * 100m / data.DeliveriesToday, 2);

        return Result<DashboardOverviewDto>.Success(new DashboardOverviewDto(
            data.OrdersToday,
            data.RevenueToday,
            data.PendingOrders,
            data.CancelledToday,
            data.ActiveProcurementBatches,
            data.DeliveriesToday,
            onTimeRate,
            data.HubInboundKgToday,
            data.HubOutboundKgToday));
    }
}
