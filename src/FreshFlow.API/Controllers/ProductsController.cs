using System.Security.Claims;
using FreshFlow.API.Extensions;
using FreshFlow.Catalog.Application.Commands.Products.Create;
using FreshFlow.Catalog.Application.Commands.Products.CreateImageUploadSignature;
using FreshFlow.Catalog.Application.Commands.Products.Deactivate;
using FreshFlow.Catalog.Application.Commands.Products.Update;
using FreshFlow.Catalog.Application.Queries.Products.GetProductById;
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
        var createdBy = Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsedId)
            ? parsedId
            : (Guid?)null;

        var result = await sender.Send(
            new CreateProductCommand(body.Name, body.UnitId, body.CategoryId, body.Description, createdBy), ct);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetProductAsync), new { id = result.Value.Id }, ApiResponse.Ok(result.Value))
            : result.Error.ToActionResult();
    }

    /// <summary>POST /api/v1/products/image/upload-signature — Admin only. Signs a Cloudinary product image upload.</summary>
    [HttpPost("image/upload-signature")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> CreateProductImageUploadSignatureAsync(CancellationToken ct)
    {
        var result = await sender.Send(new CreateProductImageUploadSignatureCommand(), ct);
        return result.IsSuccess ? Ok(ApiResponse.Ok(result.Value)) : result.Error.ToActionResult();
    }

    /// <summary>GET /api/v1/products — Returns a paged list of products.</summary>
    [HttpGet]
    [Authorize(Roles = "admin,operations_manager,market_agent,hub_staff,restaurant")]
    public async Task<IActionResult> GetProductsAsync([FromQuery] GetProductsQuery query, CancellationToken ct)
    {
        // Only admin and operations_manager may view inactive products; for all other roles
        // force IncludeInactive to false, ignoring whatever was in the query string.
        var isPrivileged = User.IsInRole("admin") || User.IsInRole("operations_manager");
        var effectiveQuery = isPrivileged ? query : query with { IncludeInactive = false };

        var result = await sender.Send(effectiveQuery, ct);
        return result.IsSuccess ? Ok(ApiResponse.Ok(result.Value)) : result.Error.ToActionResult();
    }

    /// <summary>GET /api/v1/products/{id} — Returns a single product by ID.</summary>
    [HttpGet("{id:guid}")]
    [Authorize(Roles = "admin,operations_manager,market_agent,hub_staff,restaurant")]
    public async Task<IActionResult> GetProductAsync(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new GetProductByIdQuery(id), ct);
        return result.IsSuccess ? Ok(ApiResponse.Ok(result.Value)) : result.Error.ToActionResult();
    }

    /// <summary>PUT /api/v1/products/{id} — Admin only.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> UpdateProductAsync(
        Guid id, [FromBody] UpdateProductRequest body, CancellationToken ct)
    {
        var result = await sender.Send(
            new UpdateProductCommand(
                id, body.Name, body.UnitId, body.CategoryId, body.Description, body.ImageUrl),
            ct);

        return result.IsSuccess ? Ok(ApiResponse.Ok(result.Value)) : result.Error.ToActionResult();
    }

    /// <summary>PATCH /api/v1/products/{id}/deactivate — Admin only.</summary>
    [HttpPatch("{id:guid}/deactivate")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> DeactivateProductAsync(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new DeactivateProductCommand(id), ct);
        return result.IsSuccess ? Ok(ApiResponse.Ok(result.Value)) : result.Error.ToActionResult();
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
    string? Description,
    string? ImageUrl = null);
