using System.Security.Claims;
using FreshFlow.API.Extensions;
using FreshFlow.Hub.Application.Commands.CreateHandover;
using FreshFlow.Hub.Application.Commands.DriverCheckout;
using FreshFlow.Hub.Application.Queries.ListHandovers;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FreshFlow.API.Controllers;

[ApiController]
[Route("api/v1/hubs")]
public sealed class HubHandoverController(ISender sender) : ControllerBase
{
    [HttpPost("{hubId:guid}/handover")]
    [Authorize(Roles = "hub_staff,admin,operations_manager")]
    public async Task<IActionResult> CreateHandoverAsync(
        Guid hubId,
        [FromBody] CreateHandoverRequest body,
        CancellationToken ct)
    {
        var result = await sender.Send(
            new CreateHandoverCommand(
                hubId,
                body.DeliveryRouteId,
                body.DriverUserId,
                body.OutboundEventId,
                ResolveUserId(),
                body.Notes),
            ct);

        return result.IsSuccess
            ? Created($"/api/v1/hubs/{hubId}/handover/{result.Value.HandoverId}", ApiResponse.Ok(result.Value))
            : result.Error.ToActionResult();
    }

    [HttpPost("{hubId:guid}/handover/{id:guid}/checkout")]
    [Authorize(Roles = "driver,hub_staff,admin,operations_manager")]
    public async Task<IActionResult> DriverCheckoutAsync(
        Guid hubId,
        Guid id,
        CancellationToken ct)
    {
        var result = await sender.Send(
            new DriverCheckoutCommand(hubId, id, ResolveUserId()),
            ct);

        return result.IsSuccess ? Ok(ApiResponse.Ok(result.Value)) : result.Error.ToActionResult();
    }

    [HttpGet("{hubId:guid}/handovers")]
    [Authorize(Roles = "hub_staff,admin,operations_manager")]
    public async Task<IActionResult> ListHandoversAsync(
        Guid hubId,
        [FromQuery] string? cursor = null,
        [FromQuery(Name = "page_size")] int pageSize = 50,
        CancellationToken ct = default)
    {
        var result = await sender.Send(new ListHandoversQuery(hubId, cursor, pageSize), ct);
        return result.IsSuccess
            ? Ok(ApiResponse.OkPaged(result.Value.Items, result.Value.PageSize, result.Value.NextCursor))
            : result.Error.ToActionResult();
    }

    private Guid ResolveUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? User.FindFirstValue("sub");
        return Guid.Parse(raw!);
    }
}

public sealed record CreateHandoverRequest(
    Guid DeliveryRouteId,
    Guid DriverUserId,
    Guid? OutboundEventId,
    string? Notes);
