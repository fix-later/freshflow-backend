using FreshFlow.API.Extensions;
using FreshFlow.Catalog.Application.Commands.Markets.Create;
using FreshFlow.Catalog.Application.Commands.Markets.Deactivate;
using FreshFlow.Catalog.Application.Commands.Markets.Delete;
using FreshFlow.Catalog.Application.Commands.Markets.Update;
using FreshFlow.Catalog.Application.Queries.Markets.GetMarketById;
using FreshFlow.Catalog.Application.Queries.Markets.GetMarkets;
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
        return result.IsSuccess ? Ok(result.Value) : result.Error.ToActionResult();
    }

    /// <summary>GET /api/v1/markets/{id} — any authenticated user.</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetMarketByIdAsync(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new GetMarketByIdQuery(id), ct);
        return result.IsSuccess ? Ok(result.Value) : result.Error.ToActionResult();
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
            ? CreatedAtAction(nameof(GetMarketByIdAsync), new { id = result.Value.Id }, result.Value)
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

        return result.IsSuccess ? Ok(result.Value) : result.Error.ToActionResult();
    }

    /// <summary>PATCH /api/v1/markets/{id}/deactivate — Admin only.</summary>
    [HttpPatch("{id:guid}/deactivate")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> DeactivateMarketAsync(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new DeactivateMarketCommand(id), ct);
        return result.IsSuccess ? Ok(result.Value) : result.Error.ToActionResult();
    }

    /// <summary>DELETE /api/v1/markets/{id} — Admin only (soft-delete).</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> DeleteMarketAsync(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new DeleteMarketCommand(id), ct);
        return result.IsSuccess ? NoContent() : result.Error.ToActionResult();
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
