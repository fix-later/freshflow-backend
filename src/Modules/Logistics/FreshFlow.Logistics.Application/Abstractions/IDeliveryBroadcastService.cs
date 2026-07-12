namespace FreshFlow.Logistics.Application.Abstractions;

public interface IDeliveryBroadcastService
{
    public Task BroadcastDeliveryStartedAsync(
        Guid restaurantId,
        DeliveryRealtimeUpdate update,
        CancellationToken ct);

    public Task BroadcastDeliveryStopUpdatedAsync(
        Guid restaurantId,
        DeliveryRealtimeUpdate update,
        CancellationToken ct);
}

public sealed record DeliveryRealtimeUpdate(
    Guid OrderId,
    Guid RouteId,
    Guid? DeliveryId,
    string Status,
    DateTime OccurredAt);
