using FreshFlow.API.Extensions;
using FreshFlow.Hub.Application.Commands.CreateHub;
using FreshFlow.Hub.Application.Commands.DeactivateHub;
using FreshFlow.Hub.Application.Commands.UpdateHub;
using FreshFlow.Hub.Application.Queries.GetHub;
using FreshFlow.Hub.Application.Queries.ListHubs;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FreshFlow.API.Controllers;

[ApiController]
[Route("api/v1/hubs")]
[Authorize(Roles = "admin,operations_manager")]
public sealed class HubsController(ISender sender) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> CreateHubAsync(
        [FromBody] CreateHubRequest body,
        CancellationToken ct)
    {
        var result = await sender.Send(
            new CreateHubCommand(
                body.MarketId,
                body.Name,
                body.Address,
                body.Latitude,
                body.Longitude,
                body.CapacityKg,
                body.ManagedBy),
            ct);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetHubAsync), new { id = result.Value.HubId }, ApiResponse.Ok(result.Value))
            : result.Error.ToActionResult();
    }

    [HttpGet]
    public async Task<IActionResult> ListHubsAsync(
        [FromQuery] string? cursor = null,
        [FromQuery(Name = "page_size")] int pageSize = 50,
        [FromQuery(Name = "is_active")] bool? isActive = null,
        CancellationToken ct = default)
    {
        var result = await sender.Send(new ListHubsQuery(cursor, pageSize, isActive), ct);
        return result.IsSuccess
            ? Ok(ApiResponse.OkPaged(result.Value.Items, result.Value.PageSize, result.Value.NextCursor))
            : result.Error.ToActionResult();
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetHubAsync(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new GetHubQuery(id), ct);
        return result.IsSuccess ? Ok(ApiResponse.Ok(result.Value)) : result.Error.ToActionResult();
    }

    [HttpPatch("{id:guid}")]
    public async Task<IActionResult> UpdateHubAsync(
        Guid id,
        [FromBody] UpdateHubRequest body,
        CancellationToken ct)
    {
        var result = await sender.Send(
            new UpdateHubCommand(
                id,
                body.Name,
                body.Address,
                body.Latitude,
                body.Longitude,
                body.CapacityKg,
                body.ManagedBy),
            ct);

        return result.IsSuccess ? Ok(ApiResponse.Ok(result.Value)) : result.Error.ToActionResult();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeactivateHubAsync(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new DeactivateHubCommand(id), ct);
        return result.IsSuccess ? Ok(ApiResponse.Ok(result.Value)) : result.Error.ToActionResult();
    }
}

public sealed record CreateHubRequest(
    Guid MarketId,
    string Name,
    string? Address,
    decimal? Latitude,
    decimal? Longitude,
    decimal CapacityKg,
    Guid? ManagedBy);

public sealed record UpdateHubRequest(
    string Name,
    string? Address,
    decimal? Latitude,
    decimal? Longitude,
    decimal CapacityKg,
    Guid? ManagedBy);
