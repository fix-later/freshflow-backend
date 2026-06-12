using System.Security.Claims;
using FreshFlow.API.Extensions;
using FreshFlow.Catalog.Application.Commands.Markets.Create;
using FreshFlow.Catalog.Application.Commands.Markets.Deactivate;
using FreshFlow.Catalog.Application.Commands.Markets.Delete;
using FreshFlow.Catalog.Application.Commands.Markets.Update;
using FreshFlow.Catalog.Application.Queries.Markets.GetMarketById;
using FreshFlow.Catalog.Application.Queries.Markets.GetMarkets;
using FreshFlow.Pricing.Application.Commands.UpdateAvailableQuantity;
using FreshFlow.Pricing.Application.Commands.UpdateProductPrice;
using FreshFlow.Pricing.Application.Queries.GetMarketProducts;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FreshFlow.API.Controllers;

[ApiController]
[Route("api/v1/markets")]
[Authorize]
public sealed class MarketsController(ISender sender) : ControllerBase
{
    /// <summary>GET /api/v1/markets — any authenticated user; active-only by default.</summary>
    [HttpGet]
    public async Task<IActionResult> GetMarketsAsync(
        [FromQuery] bool activeOnly = true, CancellationToken ct = default)
    {
        var result = await sender.Send(new GetMarketsQuery(activeOnly), ct);
        return result.IsSuccess ? Ok(ApiResponse.Ok(result.Value)) : result.Error.ToActionResult();
    }

    /// <summary>GET /api/v1/markets/{id} — any authenticated user.</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetMarketByIdAsync(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new GetMarketByIdQuery(id), ct);
        return result.IsSuccess ? Ok(ApiResponse.Ok(result.Value)) : result.Error.ToActionResult();
    }

    /// <summary>POST /api/v1/markets — Admin only.</summary>
    [HttpPost]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> CreateMarketAsync(
        [FromBody] CreateMarketRequest body, CancellationToken ct)
    {
        var result = await sender.Send(
            new CreateMarketCommand(body.Name, body.Location, body.Address, body.Latitude, body.Longitude), ct);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetMarketByIdAsync), new { id = result.Value.Id }, ApiResponse.Ok(result.Value))
            : result.Error.ToActionResult();
    }

    /// <summary>PUT /api/v1/markets/{id} — Admin only.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> UpdateMarketAsync(
        Guid id, [FromBody] UpdateMarketRequest body, CancellationToken ct)
    {
        var result = await sender.Send(
            new UpdateMarketCommand(id, body.Name, body.Location, body.Address, body.Latitude, body.Longitude), ct);

        return result.IsSuccess ? Ok(ApiResponse.Ok(result.Value)) : result.Error.ToActionResult();
    }

    /// <summary>PATCH /api/v1/markets/{id}/deactivate — Admin only.</summary>
    [HttpPatch("{id:guid}/deactivate")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> DeactivateMarketAsync(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new DeactivateMarketCommand(id), ct);
        return result.IsSuccess ? Ok(ApiResponse.Ok(result.Value)) : result.Error.ToActionResult();
    }

    /// <summary>DELETE /api/v1/markets/{id} — Admin only (soft-delete).</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> DeleteMarketAsync(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new DeleteMarketCommand(id), ct);
        return result.IsSuccess ? NoContent() : result.Error.ToActionResult();
    }

    /// <summary>
    /// GET /api/v1/markets/{marketId}/products
    /// Returns active products at a specific market with current price and stock.
    /// Cursor-paginated; optionally filtered by category.
    /// Any authenticated user.
    /// </summary>
    [HttpGet("{marketId:guid}/products")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMarketProductsAsync(
        Guid marketId,
        [FromQuery] string? category,
        [FromQuery] string? cursor,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var query = new GetMarketProductsQuery(marketId, category, cursor, pageSize);
        var result = await sender.Send(query, ct);

        if (!result.IsSuccess)
            return result.Error.ToActionResult();

        var page = result.Value;
        return Ok(ApiResponse.OkPaged(page.Items, page.PageSize, page.NextCursor));
    }

    /// <summary>
    /// PATCH /api/v1/markets/{marketId}/products/{productId}/price
    /// Updates the price and/or available quantity of a product at a market.
    /// Market Agent must be assigned to this market.
    /// </summary>
    [HttpPatch("{marketId:guid}/products/{productId:guid}/price")]
    [Authorize(Roles = "market_agent")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpdateProductPriceAsync(
        Guid marketId,
        Guid productId,
        [FromBody] UpdateProductPriceRequest body,
        CancellationToken ct)
    {
        if (!TryResolveAgentId(out var agentId))
            return Unauthorized(ApiResponse.Err("UNAUTHORIZED", "User ID claim is missing."));

        var command = new UpdateProductPriceCommand(
            marketId, productId, agentId, body.Price, body.Quantity, body.ExpectedVersion);

        var result = await sender.Send(command, ct);
        return result.IsSuccess ? Ok(ApiResponse.Ok(result.Value)) : result.Error.ToActionResult();
    }

    /// <summary>
    /// PATCH /api/v1/markets/{marketId}/products/{productId}/quantity
    /// Sets the available procurement quantity of a product at a market.
    /// quantity=0 is valid (marks product OUT_OF_STOCK but keeps it listed).
    /// Market Agent must be assigned to this market.
    /// </summary>
    [HttpPatch("{marketId:guid}/products/{productId:guid}/quantity")]
    [Authorize(Roles = "market_agent")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpdateAvailableQuantityAsync(
        Guid marketId,
        Guid productId,
        [FromBody] UpdateAvailableQuantityRequest body,
        CancellationToken ct)
    {
        if (!TryResolveAgentId(out var agentId))
            return Unauthorized(ApiResponse.Err("UNAUTHORIZED", "User ID claim is missing."));

        var command = new UpdateAvailableQuantityCommand(
            marketId, productId, agentId, body.Quantity, body.ExpectedVersion);

        var result = await sender.Send(command, ct);
        return result.IsSuccess ? Ok(ApiResponse.Ok(result.Value)) : result.Error.ToActionResult();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private bool TryResolveAgentId(out Guid agentId)
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? User.FindFirstValue("sub");
        return Guid.TryParse(raw, out agentId);
    }
}

// ── Request DTOs ──────────────────────────────────────────────────────────────

public sealed record CreateMarketRequest(
    string Name,
    string? Location,
    string? Address,
    decimal? Latitude,
    decimal? Longitude);

public sealed record UpdateMarketRequest(
    string Name,
    string? Location,
    string? Address,
    decimal? Latitude,
    decimal? Longitude);

public sealed record UpdateProductPriceRequest(
    decimal? Price,
    int? Quantity,
    DateTime? ExpectedVersion);

public sealed record UpdateAvailableQuantityRequest(
    int Quantity,
    DateTime? ExpectedVersion);
