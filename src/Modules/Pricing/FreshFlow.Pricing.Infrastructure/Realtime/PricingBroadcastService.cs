using FreshFlow.Pricing.Application.Abstractions;
using FreshFlow.Pricing.Application.Dtos;
using Microsoft.AspNetCore.SignalR;

namespace FreshFlow.Pricing.Infrastructure.Realtime;

/// <summary>
/// Broadcasts committed price/quantity updates to the <c>market:{marketId}</c> SignalR group.
/// Sends the <c>PriceUpdated</c> method with a <see cref="PriceUpdateBroadcastDto"/> payload;
/// clients subscribed to the group receive the update immediately after DB commit.
/// </summary>
internal sealed class PricingBroadcastService(IHubContext<PricingHub> hubContext)
    : IPricingBroadcastService
{
    private const string PriceUpdatedMethod = "PriceUpdated";

    public Task BroadcastPriceUpdateAsync(PriceUpdateBroadcastDto update, CancellationToken ct = default) =>
        hubContext.Clients
            .Group($"market:{update.MarketId}")
            .SendAsync(PriceUpdatedMethod, update, ct);
}
