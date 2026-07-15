using FreshFlow.Analytics.Application.Abstractions;
using FreshFlow.Analytics.Application.Dtos;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Analytics.Application.Queries.GetDashboardOverview;

internal sealed class GetDashboardOverviewQueryHandler(
    IDashboardOverviewReader reader,
    TimeProvider timeProvider)
    : IRequestHandler<GetDashboardOverviewQuery, Result<DashboardOverviewDto>>
{
    private static readonly TimeZoneInfo VietnamTimeZone = ResolveVietnamTimeZone();

    public async Task<Result<DashboardOverviewDto>> Handle(
        GetDashboardOverviewQuery request,
        CancellationToken ct)
    {
        var date = request.Date ?? DateOnly.FromDateTime(
            TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), VietnamTimeZone).DateTime);
        var localStart = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        var startUtc = TimeZoneInfo.ConvertTimeToUtc(localStart, VietnamTimeZone);
        var endUtc = TimeZoneInfo.ConvertTimeToUtc(localStart.AddDays(1), VietnamTimeZone);
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

    private static TimeZoneInfo ResolveVietnamTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
        }
    }
}

