using FreshFlow.Orders.Application.Dtos;

namespace FreshFlow.Orders.Application.Abstractions;

/// <summary>Broadcasts committed order status changes to subscribed SignalR clients.</summary>
public interface IOrderBroadcastService
{
    public Task BroadcastStatusChangedAsync(OrderStatusChangedBroadcastDto update, CancellationToken ct = default);
}
