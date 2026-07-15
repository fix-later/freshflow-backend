namespace FreshFlow.Analytics.Application.Abstractions;

public interface IDashboardOverviewReader
{
    public Task<DashboardOverviewReadModel> ReadAsync(
        DateOnly date,
        DateTime startUtcInclusive,
        DateTime endUtcExclusive,
        CancellationToken ct);
}

public sealed record DashboardOverviewReadModel(
    int OrdersToday,
    decimal RevenueToday,
    int PendingOrders,
    int CancelledToday,
    int ActiveProcurementBatches,
    int DeliveriesToday,
    int OnTimeDeliveriesToday,
    decimal HubInboundKgToday,
    decimal HubOutboundKgToday);

