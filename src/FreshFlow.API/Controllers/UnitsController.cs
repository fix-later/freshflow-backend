using FreshFlow.API.Extensions;
using FreshFlow.Catalog.Application.Commands.Units.Create;
using FreshFlow.Catalog.Application.Commands.Units.Deactivate;
using FreshFlow.Catalog.Application.Commands.Units.Update;
using FreshFlow.Catalog.Application.Queries.Units.GetUnitById;
using FreshFlow.Catalog.Application.Queries.Units.GetUnits;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FreshFlow.API.Controllers;

[ApiController]
[Route("api/v1/units")]
[Authorize]
public sealed class UnitsController(ISender sender) : ControllerBase
{
    /// <summary>GET /api/v1/units — any authenticated user; active-only by default.</summary>
    [HttpGet]
    public async Task<IActionResult> GetUnitsAsync(
        [FromQuery] bool activeOnly = true, CancellationToken ct = default)
    {
        var result = await sender.Send(new GetUnitsQuery(activeOnly), ct);
        return result.IsSuccess ? Ok(result.Value) : result.Error.ToActionResult();
    }

    /// <summary>GET /api/v1/units/{id} — any authenticated user.</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetUnitByIdAsync(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new GetUnitByIdQuery(id), ct);
        return result.IsSuccess ? Ok(result.Value) : result.Error.ToActionResult();
    }

    /// <summary>POST /api/v1/units — Admin only.</summary>
    [HttpPost]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> CreateUnitAsync(
        [FromBody] CreateUnitRequest body, CancellationToken ct)
    {
        var result = await sender.Send(new CreateUnitCommand(body.Name, body.Abbreviation), ct);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetUnitByIdAsync), new { id = result.Value.Id }, result.Value)
            : result.Error.ToActionResult();
    }

    /// <summary>PUT /api/v1/units/{id} — Admin only.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> UpdateUnitAsync(
        Guid id, [FromBody] UpdateUnitRequest body, CancellationToken ct)
    {
        var result = await sender.Send(new UpdateUnitCommand(id, body.Name, body.Abbreviation), ct);
        return result.IsSuccess ? Ok(result.Value) : result.Error.ToActionResult();
    }

    /// <summary>PATCH /api/v1/units/{id}/deactivate — Admin only.</summary>
    [HttpPatch("{id:guid}/deactivate")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> DeactivateUnitAsync(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new DeactivateUnitCommand(id), ct);
        return result.IsSuccess ? Ok(result.Value) : result.Error.ToActionResult();
    }
}

// ── Request DTOs ──────────────────────────────────────────────────────────────

public sealed record CreateUnitRequest(string Name, string? Abbreviation);

public sealed record UpdateUnitRequest(string Name, string? Abbreviation);
