using System.Security.Claims;
using FreshFlow.API.Extensions;
using FreshFlow.Auth.Application.Commands.UpdateMyProfile;
using FreshFlow.Auth.Application.Queries.GetMyProfile;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FreshFlow.API.Controllers;

[ApiController]
[Route("api/v1/profile")]
[Authorize]
public sealed class ProfileController(ISender sender) : ControllerBase
{
    /// <summary>GET /api/v1/profile/me — returns the authenticated user's personal profile.</summary>
    [HttpGet("me")]
    public async Task<IActionResult> GetMyProfile(CancellationToken ct)
    {
        if (!TryResolveUserId(out var userId))
            return Unauthorized(new { code = "UNAUTHORIZED", message = "User ID claim is missing or malformed." });

        var result = await sender.Send(new GetMyProfileQuery(userId), ct);
        return result.IsSuccess ? Ok(result.Value) : result.Error.ToActionResult();
    }

    /// <summary>PUT /api/v1/profile/me — updates the authenticated user's personal profile.</summary>
    [HttpPut("me")]
    public async Task<IActionResult> UpdateMyProfile(
        [FromBody] UpdateMyProfileRequest body, CancellationToken ct)
    {
        if (!TryResolveUserId(out var userId))
            return Unauthorized(new { code = "UNAUTHORIZED", message = "User ID claim is missing or malformed." });

        var result = await sender.Send(
            new UpdateMyProfileCommand(userId, body.FullName, body.Phone, body.AvatarUrl), ct);

        return result.IsSuccess ? Ok(result.Value) : result.Error.ToActionResult();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private bool TryResolveUserId(out Guid userId)
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? User.FindFirstValue("sub");
        return Guid.TryParse(raw, out userId);
    }
}

public sealed record UpdateMyProfileRequest(
    string? FullName,
    string? Phone,
    string? AvatarUrl);
