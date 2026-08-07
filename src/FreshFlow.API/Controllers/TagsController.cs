using System.Security.Claims;
using FreshFlow.API.Extensions;
using FreshFlow.Pricing.Application.Commands.CreateTag;
using FreshFlow.Pricing.Application.Commands.DeleteTag;
using FreshFlow.Pricing.Application.Commands.UpdateTag;
using FreshFlow.Pricing.Application.Queries.GetTags;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FreshFlow.API.Controllers;

/// <summary>SCRUM-386 — tag catalog CRUD. Global (not per-market).</summary>
[ApiController]
[Route("api/v1/tags")]
[Authorize]
public sealed class TagsController(ISender sender) : ControllerBase
{
    /// <summary>GET /api/v1/tags — any authenticated user.</summary>
    [HttpGet]
    public async Task<IActionResult> GetTagsAsync(CancellationToken ct)
    {
        var result = await sender.Send(new GetTagsQuery(), ct);
        return result.IsSuccess ? Ok(ApiResponse.Ok(result.Value)) : result.Error.ToActionResult();
    }

    /// <summary>POST /api/v1/tags — admin only.</summary>
    [HttpPost]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> CreateTagAsync([FromBody] CreateTagRequest body, CancellationToken ct)
    {
        TryResolveActorId(out var actorId);
        var result = await sender.Send(new CreateTagCommand(body.Name, body.PinsToTop, actorId), ct);
        return result.IsSuccess
            ? Created($"/api/v1/tags/{result.Value.Id}", ApiResponse.Ok(result.Value))
            : result.Error.ToActionResult();
    }

    /// <summary>PUT /api/v1/tags/{id} — admin only.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> UpdateTagAsync(
        Guid id, [FromBody] UpdateTagRequest body, CancellationToken ct)
    {
        TryResolveActorId(out var actorId);
        var result = await sender.Send(new UpdateTagCommand(id, body.Name, body.PinsToTop, actorId), ct);
        return result.IsSuccess ? Ok(ApiResponse.Ok(result.Value)) : result.Error.ToActionResult();
    }

    /// <summary>DELETE /api/v1/tags/{id} — admin only (soft-delete + clears assignments).</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> DeleteTagAsync(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new DeleteTagCommand(id), ct);
        return result.IsSuccess ? NoContent() : result.Error.ToActionResult();
    }

    private bool TryResolveActorId(out Guid actorId)
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(raw, out actorId);
    }
}

public sealed record CreateTagRequest(string Name, bool PinsToTop);

public sealed record UpdateTagRequest(string Name, bool PinsToTop);
