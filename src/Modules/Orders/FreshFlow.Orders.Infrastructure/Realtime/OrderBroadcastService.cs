using FreshFlow.Orders.Application.Abstractions;
using FreshFlow.Orders.Application.Dtos;
using Microsoft.AspNetCore.SignalR;

namespace FreshFlow.Orders.Infrastructure.Realtime;

/// <summary>
/// Broadcasts committed order status changes to restaurant and admin SignalR groups.
/// Sends the <c>OrderStatusChanged</c> method with a <see cref="OrderStatusChangedBroadcastDto"/> payload.
/// </summary>
internal sealed class OrderBroadcastService(IHubContext<OrderHub> hubContext)
    : IOrderBroadcastService
{
    private const string OrderStatusChangedMethod = "OrderStatusChanged";

    public async Task BroadcastStatusChangedAsync(
        OrderStatusChangedBroadcastDto update, CancellationToken ct = default)
    {
        var restaurantBroadcast = hubContext.Clients
            .Group(OrderHub.RestaurantGroupName(update.RestaurantId))
            .SendAsync(OrderStatusChangedMethod, update, ct);

        var adminBroadcast = hubContext.Clients
            .Group(OrderHub.AdminOrdersGroup)
            .SendAsync(OrderStatusChangedMethod, update, ct);

        await Task.WhenAll(restaurantBroadcast, adminBroadcast);
    }
}
