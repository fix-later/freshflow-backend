namespace FreshFlow.Hub.Infrastructure.CrossModule;

internal sealed class DeliveryRouteRow
{
    public Guid RouteId { get; init; }
    public string Status { get; init; } = string.Empty;
    public Guid? DriverUserId { get; init; }
}
