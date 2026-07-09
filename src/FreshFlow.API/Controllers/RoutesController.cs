using FreshFlow.API.Extensions;
using FreshFlow.Logistics.Application.Commands.CalculateRoute;
using FreshFlow.Logistics.Application.Commands.OptimizeRoute;
using FreshFlow.Logistics.Application.Commands.ReviewRoute;
using FreshFlow.Logistics.Application.Commands.SelectRoute;
using FreshFlow.Logistics.Application.Queries.CheckEligibility;
using FreshFlow.Logistics.Application.Queries.GetRoute;
using FreshFlow.Logistics.Application.Queries.ListRoutes;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FreshFlow.API.Controllers;

[ApiController]
[Route("api/v1/logistics/routes")]
[Authorize(Roles = "admin,operations_manager")]
public sealed class RoutesController(ISender sender) : ControllerBase
{
    [HttpPost("calculate")]
    public async Task<IActionResult> CalculateRouteAsync(
        [FromBody] CalculateRouteRequest body,
        CancellationToken ct)
    {
        var result = await sender.Send(
            new CalculateRouteCommand(
                body.SourceMarketIds,
                body.HubIds ?? [],
                body.DestinationRestaurantIds,
                body.OptimizationCriteria,
                body.ServiceDate,
                body.CompareWithHub ?? false),
            ct);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetRouteAsync), new { id = result.Value.Id }, ApiResponse.Ok(result.Value))
            : result.Error.ToActionResult();
    }

    [HttpPost("{id:guid}/select")]
    public async Task<IActionResult> SelectRouteAsync(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new SelectRouteCommand(id), ct);
        return result.IsSuccess ? Ok(ApiResponse.Ok(result.Value)) : result.Error.ToActionResult();
    }

    [HttpPost("{id:guid}/optimize")]
    public async Task<IActionResult> OptimizeRouteAsync(
        Guid id,
        [FromBody] OptimizeRouteRequest body,
        CancellationToken ct)
    {
        var result = await sender.Send(new OptimizeRouteCommand(id, body.OptimizationCriteria), ct);
        return result.IsSuccess ? Ok(ApiResponse.Ok(result.Value)) : result.Error.ToActionResult();
    }

    [HttpPost("{id:guid}/review")]
    public async Task<IActionResult> ReviewRouteAsync(
        Guid id,
        [FromBody] ReviewRouteRequest? body,
        CancellationToken ct)
    {
        var result = await sender.Send(new ReviewRouteCommand(id, body?.StopOrder), ct);
        return result.IsSuccess ? Ok(ApiResponse.Ok(result.Value)) : result.Error.ToActionResult();
    }

    [HttpGet]
    public async Task<IActionResult> ListRoutesAsync(
        [FromQuery] string? cursor = null,
        [FromQuery(Name = "page_size")] int pageSize = 50,
        [FromQuery(Name = "service_date")] DateOnly? serviceDate = null,
        [FromQuery] string? status = null,
        CancellationToken ct = default)
    {
        var result = await sender.Send(new ListRoutesQuery(cursor, pageSize, serviceDate, status), ct);
        return result.IsSuccess
            ? Ok(ApiResponse.OkPaged(result.Value.Items, result.Value.PageSize, result.Value.NextCursor))
            : result.Error.ToActionResult();
    }

    [HttpGet("{routeId:guid}/eligibility")]
    public async Task<IActionResult> CheckEligibilityAsync(
        Guid routeId,
        [FromQuery] Guid vehicleId,
        [FromQuery(Name = "driver_user_id")] Guid? driverUserId,
        CancellationToken ct)
    {
        var result = await sender.Send(new CheckEligibilityQuery(routeId, vehicleId, driverUserId), ct);
        return result.IsSuccess ? Ok(ApiResponse.Ok(result.Value)) : result.Error.ToActionResult();
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetRouteAsync(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new GetRouteQuery(id), ct);
        return result.IsSuccess ? Ok(ApiResponse.Ok(result.Value)) : result.Error.ToActionResult();
    }
}

public sealed record CalculateRouteRequest(
    IReadOnlyList<Guid> SourceMarketIds,
    IReadOnlyList<Guid>? HubIds,
    IReadOnlyList<Guid> DestinationRestaurantIds,
    string? OptimizationCriteria,
    DateOnly ServiceDate,
    bool? CompareWithHub);

public sealed record OptimizeRouteRequest(string OptimizationCriteria);

public sealed record ReviewRouteRequest(IReadOnlyList<Guid>? StopOrder);
