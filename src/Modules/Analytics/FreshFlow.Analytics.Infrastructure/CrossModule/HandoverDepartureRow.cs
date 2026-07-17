namespace FreshFlow.Analytics.Infrastructure.CrossModule;

internal sealed class HandoverDepartureRow
{
    public Guid DeliveryRouteId { get; init; }
    public DateTime DriverConfirmedAt { get; init; }
}
