using FreshFlow.Logistics.Application.Abstractions;
using Microsoft.AspNetCore.SignalR;

namespace FreshFlow.Logistics.Infrastructure.Realtime;

internal sealed class DeliveryBroadcastService(IHubContext<DeliveryHub> hubContext)
    : IDeliveryBroadcastService
{
    private const string DeliveryStartedMethod = "DeliveryStarted";
    private const string DeliveryStopUpdatedMethod = "DeliveryStopUpdated";

    public async Task BroadcastDeliveryStartedAsync(
        Guid restaurantId,
        DeliveryRealtimeUpdate update,
        CancellationToken ct)
    {
        await BroadcastAsync(restaurantId, DeliveryStartedMethod, update, ct);
    }

    public async Task BroadcastDeliveryStopUpdatedAsync(
        Guid restaurantId,
        DeliveryRealtimeUpdate update,
        CancellationToken ct)
    {
        await BroadcastAsync(restaurantId, DeliveryStopUpdatedMethod, update, ct);
    }

    private async Task BroadcastAsync(
        Guid restaurantId,
        string methodName,
        DeliveryRealtimeUpdate update,
        CancellationToken ct)
    {
        var restaurantBroadcast = hubContext.Clients
            .Group(DeliveryHub.RestaurantGroupName(restaurantId))
            .SendAsync(methodName, update, ct);

        var adminBroadcast = hubContext.Clients
            .Group(DeliveryHub.AdminDeliveryGroup)
            .SendAsync(methodName, update, ct);

        await Task.WhenAll(restaurantBroadcast, adminBroadcast);
    }
}
