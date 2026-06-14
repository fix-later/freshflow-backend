using FreshFlow.Pricing.Application.Abstractions;
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
public sealed class PricingHub(IAssignedMarketReader marketReader) : Hub
{
    // Matches the role value written verbatim into the JWT "role" claim by JwtTokenService.
    private const string AdminRole = "admin";

    /// <summary>
    /// Adds the caller to the market group to receive targeted price broadcasts.
    /// Requires that the caller is either an admin or has an active, non-revoked
    /// assignment to <paramref name="marketId"/>; otherwise throws <see cref="HubException"/>.
    /// </summary>
    public async Task JoinMarketAsync(string marketId)
    {
        await AuthorizeMarketAccessAsync(marketId);
        await Groups.AddToGroupAsync(Context.ConnectionId, $"market:{marketId}");
    }

    /// <summary>
    /// Removes the caller from a market group.
    /// Applies the same authorization check as <see cref="JoinMarketAsync"/> so that
    /// clients cannot silently leave markets they are not supposed to know about.
    /// </summary>
    public async Task LeaveMarketAsync(string marketId)
    {
        await AuthorizeMarketAccessAsync(marketId);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"market:{marketId}");
    }

    // ── Private ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Validates that the connected user may access <paramref name="marketId"/>.
    /// Admins bypass the per-market check. All other roles require an active
    /// (non-soft-deleted) <c>user_market_assignments</c> row.
    /// </summary>
    private async Task AuthorizeMarketAccessAsync(string marketId)
    {
        // Admins may subscribe to any market without an explicit assignment.
        if (Context.User?.IsInRole(AdminRole) == true)
            return;

        if (!Guid.TryParse(marketId, out var marketGuid))
            throw new HubException($"Invalid market identifier: '{marketId}'.");

        if (!Guid.TryParse(Context.User?.FindFirst("sub")?.Value, out var userId))
            throw new HubException("Unable to determine caller identity.");

        var hasAccess = await marketReader.HasAssignmentAsync(
            userId, marketGuid, Context.ConnectionAborted);

        if (!hasAccess)
            throw new HubException($"Access to market '{marketId}' is denied.");
    }
}
