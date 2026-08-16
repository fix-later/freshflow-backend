using System.Security.Claims;
using FreshFlow.API.Extensions;
using FreshFlow.Hub.Application.Commands.CreateHandover;
using FreshFlow.Hub.Application.Commands.DriverCheckout;
using FreshFlow.Hub.Application.Queries.ListHandovers;
using FreshFlow.Logistics.Application.Queries.ListEligibleDrivers;
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
                body.Notes,
                BypassHubAssignment()),
            ct);

        return result.IsSuccess
            ? Created($"/api/v1/hubs/{hubId}/handover/{result.Value.HandoverId}", ApiResponse.Ok(result.Value))
            : result.Error.ToActionResult();
    }

    [HttpPost("{hubId:guid}/handover/{id:guid}/checkout")]
    [Authorize(Roles = "driver")]
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
        var result = await sender.Send(
            new ListHandoversQuery(
                hubId,
                cursor,
                pageSize,
                ResolveUserId(),
                BypassHubAssignment()),
            ct);
        return result.IsSuccess
            ? Ok(ApiResponse.OkPaged(result.Value.Items, result.Value.PageSize, result.Value.NextCursor))
            : result.Error.ToActionResult();
    }

    [HttpGet("{hubId:guid}/drivers/eligible")]
    [Authorize(Roles = "hub_staff,admin,operations_manager")]
    public async Task<IActionResult> ListEligibleDriversAsync(Guid hubId, CancellationToken ct)
    {
        var result = await sender.Send(new ListEligibleDriversQuery(hubId), ct);
        return result.IsSuccess ? Ok(ApiResponse.Ok(result.Value)) : result.Error.ToActionResult();
    }

    private Guid ResolveUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? User.FindFirstValue("sub");
        return Guid.Parse(raw!);
    }

    private bool BypassHubAssignment() =>
        User.IsInRole("admin") || User.IsInRole("operations_manager");
}

public sealed record CreateHandoverRequest(
    Guid DeliveryRouteId,
    Guid DriverUserId,
    Guid? OutboundEventId,
    string? Notes);
