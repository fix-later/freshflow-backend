using FreshFlow.API.Extensions;
using FreshFlow.Catalog.Application.Commands.Categories.Activate;
using FreshFlow.Catalog.Application.Commands.Categories.Create;
using FreshFlow.Catalog.Application.Commands.Categories.CreateImageUploadSignature;
using FreshFlow.Catalog.Application.Commands.Categories.Deactivate;
using FreshFlow.Catalog.Application.Commands.Categories.Update;
using FreshFlow.Catalog.Application.Queries.Categories.GetCategories;
using FreshFlow.Catalog.Application.Queries.Categories.GetCategoryById;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FreshFlow.API.Controllers;

[ApiController]
[Route("api/v1/categories")]
[Authorize]
public sealed class CategoriesController(ISender sender) : ControllerBase
{
    /// <summary>GET /api/v1/categories — any authenticated user; active-only by default.</summary>
    [HttpGet]
    public async Task<IActionResult> GetCategoriesAsync(
        [FromQuery] bool activeOnly = true, CancellationToken ct = default)
    {
        var result = await sender.Send(new GetCategoriesQuery(activeOnly), ct);
        return result.IsSuccess ? Ok(ApiResponse.Ok(result.Value)) : result.Error.ToActionResult();
    }

    /// <summary>GET /api/v1/categories/{id} — any authenticated user.</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetCategoryByIdAsync(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new GetCategoryByIdQuery(id), ct);
        return result.IsSuccess ? Ok(ApiResponse.Ok(result.Value)) : result.Error.ToActionResult();
    }

    /// <summary>POST /api/v1/categories — Admin only.</summary>
    [HttpPost]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> CreateCategoryAsync(
        [FromBody] CreateCategoryRequest body, CancellationToken ct)
    {
        var result = await sender.Send(new CreateCategoryCommand(body.Name, body.ParentId, body.ImageUrl), ct);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetCategoryByIdAsync), new { id = result.Value.Id }, ApiResponse.Ok(result.Value))
            : result.Error.ToActionResult();
    }

    /// <summary>POST /api/v1/categories/image/upload-signature — Admin only.</summary>
    [HttpPost("image/upload-signature")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> CreateCategoryImageUploadSignatureAsync(CancellationToken ct)
    {
        var result = await sender.Send(new CreateCategoryImageUploadSignatureCommand(), ct);
        return result.IsSuccess ? Ok(ApiResponse.Ok(result.Value)) : result.Error.ToActionResult();
    }

    /// <summary>PUT /api/v1/categories/{id} — Admin only.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> UpdateCategoryAsync(
        Guid id, [FromBody] UpdateCategoryRequest body, CancellationToken ct)
    {
        var result = await sender.Send(new UpdateCategoryCommand(id, body.Name, body.ParentId, body.ImageUrl), ct);
        return result.IsSuccess ? Ok(ApiResponse.Ok(result.Value)) : result.Error.ToActionResult();
    }

    /// <summary>PATCH /api/v1/categories/{id}/deactivate — Admin only.</summary>
    [HttpPatch("{id:guid}/deactivate")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> DeactivateCategoryAsync(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new DeactivateCategoryCommand(id), ct);
        return result.IsSuccess ? Ok(ApiResponse.Ok(result.Value)) : result.Error.ToActionResult();
    }

    /// <summary>PATCH /api/v1/categories/{id}/activate — Admin only.</summary>
    [HttpPatch("{id:guid}/activate")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> ActivateCategoryAsync(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new ActivateCategoryCommand(id), ct);
        return result.IsSuccess ? Ok(ApiResponse.Ok(result.Value)) : result.Error.ToActionResult();
    }
}

// ── Request DTOs ──────────────────────────────────────────────────────────────

public sealed record CreateCategoryRequest(string Name, Guid? ParentId, string? ImageUrl);

public sealed record UpdateCategoryRequest(string Name, Guid? ParentId, string? ImageUrl);
