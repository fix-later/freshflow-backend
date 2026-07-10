using System.Security.Claims;
using FreshFlow.API.Extensions;
using FreshFlow.Logistics.Application.Commands.DeactivateVehicle;
using FreshFlow.Logistics.Application.Commands.RegisterVehicle;
using FreshFlow.Logistics.Application.Commands.UpdateVehicle;
using FreshFlow.Logistics.Application.Queries.GetVehicle;
using FreshFlow.Logistics.Application.Queries.ListVehicles;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FreshFlow.API.Controllers;

[ApiController]
[Route("api/v1/logistics/vehicles")]
[Authorize(Roles = "admin,operations_manager")]
public sealed class VehiclesController(ISender sender) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> RegisterVehicleAsync(
        [FromBody] RegisterVehicleRequest body,
        CancellationToken ct)
    {
        var registeredBy = Guid.TryParse(
            User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"),
            out var id)
            ? id
            : (Guid?)null;

        var result = await sender.Send(
            new RegisterVehicleCommand(body.PlateNumber, body.CapacityKg, body.VehicleType, registeredBy),
            ct);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetVehicleAsync), new { id = result.Value.Id }, ApiResponse.Ok(result.Value))
            : result.Error.ToActionResult();
    }

    [HttpGet]
    public async Task<IActionResult> ListVehiclesAsync(
        [FromQuery] string? cursor = null,
        [FromQuery(Name = "page_size")] int pageSize = 50,
        [FromQuery(Name = "is_active")] bool? isActive = null,
        CancellationToken ct = default)
    {
        var result = await sender.Send(new ListVehiclesQuery(cursor, pageSize, isActive), ct);
        return result.IsSuccess
            ? Ok(ApiResponse.OkPaged(result.Value.Items, result.Value.PageSize, result.Value.NextCursor))
            : result.Error.ToActionResult();
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetVehicleAsync(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new GetVehicleQuery(id), ct);
        return result.IsSuccess ? Ok(ApiResponse.Ok(result.Value)) : result.Error.ToActionResult();
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateVehicleAsync(
        Guid id,
        [FromBody] UpdateVehicleRequest body,
        CancellationToken ct)
    {
        var result = await sender.Send(
            new UpdateVehicleCommand(id, body.PlateNumber, body.CapacityKg, body.VehicleType),
            ct);

        return result.IsSuccess ? Ok(ApiResponse.Ok(result.Value)) : result.Error.ToActionResult();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeactivateVehicleAsync(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new DeactivateVehicleCommand(id), ct);
        return result.IsSuccess ? Ok(ApiResponse.Ok(result.Value)) : result.Error.ToActionResult();
    }
}

public sealed record RegisterVehicleRequest(
    string PlateNumber,
    decimal CapacityKg,
    string VehicleType);

public sealed record UpdateVehicleRequest(
    string PlateNumber,
    decimal CapacityKg,
    string VehicleType);
