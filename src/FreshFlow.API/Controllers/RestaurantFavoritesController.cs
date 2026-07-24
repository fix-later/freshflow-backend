using System.Security.Claims;
using FreshFlow.API.Extensions;
using FreshFlow.Orders.Application.Commands.Favorites.Add;
using FreshFlow.Orders.Application.Commands.Favorites.Remove;
using FreshFlow.Orders.Application.Queries.GetFavorites;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FreshFlow.API.Controllers;

/// <summary>SCRUM-368 — server-backed restaurant favorites (wishlist). Separate from
/// RestaurantProfileController to avoid mixing Auth and Orders DTOs in one file.</summary>
[ApiController]
[Route("api/v1/restaurants")]
[Authorize(Roles = "restaurant")]
public sealed class RestaurantFavoritesController(ISender sender) : ControllerBase
{
    /// <summary>GET /api/v1/restaurants/me/favorites — lists the restaurant's favorites, enriched.</summary>
    [HttpGet("me/favorites")]
    public async Task<IActionResult> GetFavoritesAsync(CancellationToken ct)
    {
        var result = await sender.Send(new GetFavoritesQuery(ResolveUserId()), ct);
        return result.IsSuccess ? Ok(ApiResponse.Ok(result.Value)) : result.Error.ToActionResult();
    }

    /// <summary>POST /api/v1/restaurants/me/favorites — adds a favorite (idempotent).</summary>
    [HttpPost("me/favorites")]
    public async Task<IActionResult> AddFavoriteAsync(
        [FromBody] AddFavoriteRequest body, CancellationToken ct)
    {
        var result = await sender.Send(
            new AddFavoriteCommand(ResolveUserId(), body.MarketProductId), ct);

        return result.IsSuccess ? Ok(ApiResponse.Ok(result.Value)) : result.Error.ToActionResult();
    }

    /// <summary>
    /// DELETE /api/v1/restaurants/me/favorites/{marketProductId} — removes a favorite
    /// (idempotent — already-removed is also a 204).
    /// </summary>
    [HttpDelete("me/favorites/{marketProductId:guid}")]
    public async Task<IActionResult> RemoveFavoriteAsync(Guid marketProductId, CancellationToken ct)
    {
        var result = await sender.Send(
            new RemoveFavoriteCommand(ResolveUserId(), marketProductId), ct);

        return result.IsSuccess ? NoContent() : result.Error.ToActionResult();
    }

    private Guid ResolveUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? User.FindFirstValue("sub");

        return Guid.TryParse(raw, out var id)
            ? id
            : throw new UnauthorizedAccessException("User ID claim is missing or malformed.");
    }
}

public sealed record AddFavoriteRequest(Guid MarketProductId);
