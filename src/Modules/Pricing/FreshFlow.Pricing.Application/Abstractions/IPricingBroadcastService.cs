using FreshFlow.Pricing.Application.Dtos;

namespace FreshFlow.Pricing.Application.Abstractions;

/// <summary>Broadcasts committed price/quantity updates to subscribed SignalR clients.</summary>
public interface IPricingBroadcastService
{
    public Task BroadcastPriceUpdateAsync(PriceUpdateBroadcastDto update, CancellationToken ct = default);
}
