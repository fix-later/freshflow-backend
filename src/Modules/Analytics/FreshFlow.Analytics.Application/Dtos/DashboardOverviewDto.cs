namespace FreshFlow.Analytics.Application.Dtos;

public sealed record DashboardOverviewDto(
    int OrdersToday,
    decimal RevenueToday,
    int PendingOrders,
    int CancelledToday,
    int ActiveProcurementBatches,
    int DeliveriesToday,
    decimal OnTimeRatePercent,
    decimal HubInboundKgToday,
    decimal HubOutboundKgToday);

