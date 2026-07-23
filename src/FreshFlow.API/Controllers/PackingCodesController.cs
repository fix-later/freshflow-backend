using FreshFlow.API.Extensions;
using FreshFlow.Catalog.Application.Commands.PackingCodes.Create;
using FreshFlow.Catalog.Application.Commands.PackingCodes.Deactivate;
using FreshFlow.Catalog.Application.Commands.PackingCodes.Update;
using FreshFlow.Catalog.Application.Queries.PackingCodes.GetById;
using FreshFlow.Catalog.Application.Queries.PackingCodes.List;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FreshFlow.API.Controllers;

[ApiController]
[Route("api/v1/catalog/packing-codes")]
[Authorize(Roles = "admin")]
public sealed class PackingCodesController(ISender sender) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> CreatePackingCodeAsync(
        [FromBody] CreatePackingCodeRequest body, CancellationToken ct)
    {
        var result = await sender.Send(
            new CreatePackingCodeCommand(body.Code, body.Description, body.CapacityKg), ct);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetPackingCodeByIdAsync),
                new { id = result.Value.Id }, ApiResponse.Ok(result.Value))
            : result.Error.ToActionResult();
    }

    [HttpGet]
    public async Task<IActionResult> ListPackingCodesAsync(
        [FromQuery] bool activeOnly = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await sender.Send(
            new ListPackingCodesQuery(activeOnly, page, pageSize), ct);
        return result.IsSuccess ? Ok(ApiResponse.Ok(result.Value)) : result.Error.ToActionResult();
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetPackingCodeByIdAsync(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new GetPackingCodeByIdQuery(id), ct);
        return result.IsSuccess ? Ok(ApiResponse.Ok(result.Value)) : result.Error.ToActionResult();
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdatePackingCodeAsync(
        Guid id, [FromBody] UpdatePackingCodeRequest body, CancellationToken ct)
    {
        var result = await sender.Send(
            new UpdatePackingCodeCommand(id, body.Code, body.Description, body.CapacityKg), ct);
        return result.IsSuccess ? Ok(ApiResponse.Ok(result.Value)) : result.Error.ToActionResult();
    }

    [HttpPatch("{id:guid}/deactivate")]
    public async Task<IActionResult> DeactivatePackingCodeAsync(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new DeactivatePackingCodeCommand(id), ct);
        return result.IsSuccess ? Ok(ApiResponse.Ok(result.Value)) : result.Error.ToActionResult();
    }
}

public sealed record CreatePackingCodeRequest(
    string Code,
    string? Description,
    decimal CapacityKg);

public sealed record UpdatePackingCodeRequest(
    string Code,
    string? Description,
    decimal CapacityKg);
