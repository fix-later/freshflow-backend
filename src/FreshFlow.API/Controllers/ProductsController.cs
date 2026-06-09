using System.Security.Claims;
using FreshFlow.API.Extensions;
using FreshFlow.Catalog.Application.Commands.Products.Create;
using FreshFlow.Catalog.Application.Commands.Products.Deactivate;
using FreshFlow.Catalog.Application.Commands.Products.Update;
using FreshFlow.Catalog.Application.Queries.Products.GetProducts;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FreshFlow.API.Controllers;

[ApiController]
[Route("api/v1/products")]
[Authorize]   // All endpoints require authentication; roles are enforced per-action below.
public sealed class ProductsController(ISender sender) : ControllerBase
{
    /// <summary>POST /api/v1/products — Admin only. Market Agents are explicitly blocked (403).</summary>
    [HttpPost]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> CreateProductAsync(
        [FromBody] CreateProductRequest body, CancellationToken ct)
    {
        var createdBy = User.FindFirstValue(ClaimTypes.NameIdentifier) is { } sub
            ? Guid.Parse(sub)
            : (Guid?)null;

        var result = await sender.Send(
            new CreateProductCommand(body.Name, body.UnitId, body.CategoryId, body.Description, createdBy), ct);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetProductAsync), new { id = result.Value.Id }, result.Value)
            : result.Error.ToActionResult();
    }

    /// <summary>GET /api/v1/products — Returns a paged list of products.</summary>
    [HttpGet]
    [Authorize(Roles = "admin,operations_manager,market_agent,hub_staff,restaurant")]
    public async Task<IActionResult> GetProductsAsync([FromQuery] GetProductsQuery query, CancellationToken ct)
    {
        var result = await sender.Send(query, ct);
        return result.IsSuccess ? Ok(result.Value) : result.Error.ToActionResult();
    }

    /// <summary>GET /api/v1/products/{id} — placeholder; implemented in view tasks.</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetProductAsync(Guid id, CancellationToken ct)
    {
        // Implemented in UC-CAT-03
        await Task.CompletedTask;
        return NotFound(new { code = "NOT_IMPLEMENTED", message = "Use view tasks." });
    }

    /// <summary>PUT /api/v1/products/{id} — Admin only.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> UpdateProductAsync(
        Guid id, [FromBody] UpdateProductRequest body, CancellationToken ct)
    {
        var result = await sender.Send(
            new UpdateProductCommand(id, body.Name, body.UnitId, body.CategoryId, body.Description), ct);

        return result.IsSuccess ? Ok(result.Value) : result.Error.ToActionResult();
    }

    /// <summary>PATCH /api/v1/products/{id}/deactivate — Admin only.</summary>
    [HttpPatch("{id:guid}/deactivate")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> DeactivateProductAsync(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new DeactivateProductCommand(id), ct);
        return result.IsSuccess ? Ok(result.Value) : result.Error.ToActionResult();
    }
}

// ── Request DTOs ──────────────────────────────────────────────────────────────

public sealed record CreateProductRequest(
    string Name,
    Guid UnitId,
    Guid? CategoryId,
    string? Description);

public sealed record UpdateProductRequest(
    string Name,
    Guid UnitId,
    Guid? CategoryId,
    string? Description);
