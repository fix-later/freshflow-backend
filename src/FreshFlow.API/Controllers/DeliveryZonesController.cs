using FreshFlow.API.Extensions;
using FreshFlow.Logistics.Application.Commands.DeliveryZones.Create;
using FreshFlow.Logistics.Application.Commands.DeliveryZones.Deactivate;
using FreshFlow.Logistics.Application.Commands.DeliveryZones.Update;
using FreshFlow.Logistics.Application.Queries.DeliveryZones.GetDeliveryZoneById;
using FreshFlow.Logistics.Application.Queries.DeliveryZones.GetDeliveryZones;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FreshFlow.API.Controllers;

[ApiController]
[Route("api/v1/logistics/delivery-zones")]
[Authorize(Roles = "admin,operations_manager")]
public sealed class DeliveryZonesController(ISender sender) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> CreateDeliveryZoneAsync(
        [FromBody] CreateDeliveryZoneRequest body,
        CancellationToken ct)
    {
        var result = await sender.Send(
            new CreateDeliveryZoneCommand(body.Code, body.Name, body.Description),
            ct);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetDeliveryZoneAsync), new { id = result.Value.Id }, ApiResponse.Ok(result.Value))
            : result.Error.ToActionResult();
    }

    [HttpGet]
    public async Task<IActionResult> ListDeliveryZonesAsync(
        [FromQuery(Name = "active_only")] bool activeOnly = true,
        CancellationToken ct = default)
    {
        var result = await sender.Send(new GetDeliveryZonesQuery(activeOnly), ct);
        return result.IsSuccess ? Ok(ApiResponse.Ok(result.Value)) : result.Error.ToActionResult();
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetDeliveryZoneAsync(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new GetDeliveryZoneByIdQuery(id), ct);
        return result.IsSuccess ? Ok(ApiResponse.Ok(result.Value)) : result.Error.ToActionResult();
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateDeliveryZoneAsync(
        Guid id,
        [FromBody] UpdateDeliveryZoneRequest body,
        CancellationToken ct)
    {
        var result = await sender.Send(
            new UpdateDeliveryZoneCommand(id, body.Name, body.Description),
            ct);

        return result.IsSuccess ? Ok(ApiResponse.Ok(result.Value)) : result.Error.ToActionResult();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeactivateDeliveryZoneAsync(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new DeactivateDeliveryZoneCommand(id), ct);
        return result.IsSuccess ? Ok(ApiResponse.Ok(result.Value)) : result.Error.ToActionResult();
    }
}

public sealed record CreateDeliveryZoneRequest(string Code, string Name, string? Description);

public sealed record UpdateDeliveryZoneRequest(string Name, string? Description);
