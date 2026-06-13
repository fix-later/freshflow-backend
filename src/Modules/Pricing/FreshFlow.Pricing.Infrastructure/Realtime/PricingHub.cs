using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace FreshFlow.Pricing.Infrastructure.Realtime;

/// <summary>
/// Real-time hub for price and quantity updates (UC-PRI-08).
///
/// Clients join the group <c>market:{marketId}</c> to receive updates for a specific market.
/// JWT is supplied via the <c>access_token</c> query string during negotiate;
/// the Auth module's <c>JwtBearerEvents.OnMessageReceived</c> handler processes it.
///
/// Architecture note: the hub lives in <c>Pricing.Infrastructure</c> (rather than
/// <c>FreshFlow.API/SignalR/</c>) so that <see cref="PricingBroadcastService"/> can reference
/// <c>IHubContext&lt;PricingHub&gt;</c> without creating a circular project dependency
/// (API → Infrastructure is one-directional).
/// </summary>
[Authorize]
public sealed class PricingHub : Hub
{
    /// <summary>Adds the caller to the market group to receive targeted price broadcasts.</summary>
    public Task JoinMarketAsync(string marketId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, $"market:{marketId}");

    /// <summary>Removes the caller from a market group.</summary>
    public Task LeaveMarketAsync(string marketId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, $"market:{marketId}");
}
