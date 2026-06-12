using System.Security.Claims;
using FreshFlow.API.Extensions;
using FreshFlow.Pricing.Application.Queries.GetAssignedMarkets;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FreshFlow.API.Controllers;

[ApiController]
[Route("api/v1/pricing")]
[Authorize]
public sealed class PricingController(ISender sender) : ControllerBase
{
    /// <summary>
    /// GET /api/v1/pricing/assigned-markets
    /// Returns the list of markets the calling market agent is assigned to.
    /// </summary>
    [HttpGet("assigned-markets")]
    [Authorize(Roles = "market_agent")]
    public async Task<IActionResult> GetAssignedMarketsAsync(CancellationToken ct)
    {
        if (!TryResolveUserId(out var userId))
            return Unauthorized(ApiResponse.Err("UNAUTHORIZED", "User ID claim is missing or malformed."));

        var result = await sender.Send(new GetAssignedMarketsQuery(userId), ct);
        return result.IsSuccess ? Ok(ApiResponse.Ok(result.Value)) : result.Error.ToActionResult();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private bool TryResolveUserId(out Guid userId)
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? User.FindFirstValue("sub");
        return Guid.TryParse(raw, out userId);
    }
}
