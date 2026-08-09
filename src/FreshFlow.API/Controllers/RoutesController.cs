using System.Security.Claims;
using FreshFlow.API.Extensions;
using FreshFlow.Hub.Application.Abstractions;
using FreshFlow.Hub.Application.Services;
using FreshFlow.Logistics.Application.Commands.ApproveRoutePlan;
using FreshFlow.Logistics.Application.Commands.AssignVehicle;
using FreshFlow.Logistics.Application.Commands.CalculateRoute;
using FreshFlow.Logistics.Application.Commands.OptimizeRoute;
using FreshFlow.Logistics.Application.Commands.PlanRoutes;
using FreshFlow.Logistics.Application.Commands.ReviewRoute;
using FreshFlow.Logistics.Application.Commands.SelectRoute;
using FreshFlow.Logistics.Application.Queries.CheckEligibility;
using FreshFlow.Logistics.Application.Queries.GetLoadingManifest;
using FreshFlow.Logistics.Application.Queries.GetRoute;
using FreshFlow.Logistics.Application.Queries.GetRouteDeliveries;
using FreshFlow.Logistics.Application.Queries.GetRoutePlan;
using FreshFlow.Logistics.Application.Queries.GetRouteSuggestions;
using FreshFlow.Logistics.Application.Queries.ListRoutes;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace FreshFlow.API.Controllers;

[ApiController]
[Route("api/v1/logistics/routes")]
public sealed class RoutesController(ISender sender) : ControllerBase
{
    [HttpPost("calculate")]
    [Authorize(Roles = "admin,operations_manager")]
    public async Task<IActionResult> CalculateRouteAsync(
        [FromBody] CalculateRouteRequest body,
        CancellationToken ct)
    {
        var result = await sender.Send(
            new CalculateRouteCommand(
                body.HubId,
                body.DestinationRestaurantIds,
                body.OptimizationCriteria,
                body.ServiceDate),
            ct);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetRouteAsync), new { id = result.Value.Id }, ApiResponse.Ok(result.Value))
            : result.Error.ToActionResult();
    }

    [HttpPost("plan")]
    [Authorize(Roles = "admin,operations_manager")]
    public async Task<IActionResult> PlanRoutesAsync(
        [FromBody] PlanRoutesRequest body,
        CancellationToken ct)
    {
        var result = await sender.Send(
            new PlanRoutesCommand(body.HubId, body.ServiceDate, body.OptimizationCriteria),
            ct);
        return result.IsSuccess ? Ok(ApiResponse.Ok(result.Value)) : result.Error.ToActionResult();
    }

    [HttpPost("plans/{planId:guid}/approve")]
    [Authorize(Roles = "admin,operations_manager")]
    public async Task<IActionResult> ApproveRoutePlanAsync(Guid planId, CancellationToken ct)
    {
        var result = await sender.Send(new ApproveRoutePlanCommand(planId), ct);
        return result.IsSuccess ? Ok(ApiResponse.Ok(result.Value)) : result.Error.ToActionResult();
    }

    [HttpGet("plans/{planId:guid}")]
    [Authorize(Roles = "admin,operations_manager")]
    public async Task<IActionResult> GetRoutePlanAsync(Guid planId, CancellationToken ct)
    {
        var result = await sender.Send(new GetRoutePlanQuery(planId), ct);
        return result.IsSuccess ? Ok(ApiResponse.Ok(result.Value)) : result.Error.ToActionResult();
    }

    [HttpPost("{id:guid}/select")]
    [Authorize(Roles = "admin,operations_manager")]
    public async Task<IActionResult> SelectRouteAsync(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new SelectRouteCommand(id), ct);
        return result.IsSuccess ? Ok(ApiResponse.Ok(result.Value)) : result.Error.ToActionResult();
    }

    [HttpPost("{id:guid}/optimize")]
    [Authorize(Roles = "admin,operations_manager")]
    public async Task<IActionResult> OptimizeRouteAsync(
        Guid id,
        [FromBody] OptimizeRouteRequest body,
        CancellationToken ct)
    {
        var result = await sender.Send(new OptimizeRouteCommand(id, body.OptimizationCriteria), ct);
        return result.IsSuccess ? Ok(ApiResponse.Ok(result.Value)) : result.Error.ToActionResult();
    }

    [HttpPost("{id:guid}/review")]
    [Authorize(Roles = "admin,operations_manager")]
    public async Task<IActionResult> ReviewRouteAsync(
        Guid id,
        [FromBody] ReviewRouteRequest? body,
        CancellationToken ct)
    {
        var result = await sender.Send(new ReviewRouteCommand(id, body?.StopOrder), ct);
        return result.IsSuccess ? Ok(ApiResponse.Ok(result.Value)) : result.Error.ToActionResult();
    }

    [HttpPost("{id:guid}/assign-vehicle")]
    [Authorize(Roles = "admin,operations_manager,hub_staff")]
    // Intentionally NOT narrowed: hub_staff dispatch their own last-mile (hub-dispatch model). The
    // handler still enforces eligibility, capacity, double-booking, and route-state transition guards.
    public async Task<IActionResult> AssignVehicleAsync(
        Guid id,
        [FromBody] AssignVehicleRequest body,
        CancellationToken ct)
    {
        var denied = await CheckRouteAccessAsync(id, ct);
        if (denied is not null)
            return denied.ToActionResult();

        var result = await sender.Send(new AssignVehicleCommand(id, body.VehicleId, body.DriverUserId), ct);
        return result.IsSuccess ? Ok(ApiResponse.Ok(result.Value)) : result.Error.ToActionResult();
    }

    [HttpGet]
    [Authorize(Roles = "admin,operations_manager,hub_staff")]
    public async Task<IActionResult> ListRoutesAsync(
        [FromQuery] string? cursor = null,
        [FromQuery(Name = "page_size")] int pageSize = 50,
        [FromQuery(Name = "service_date")] DateOnly? serviceDate = null,
        [FromQuery] string? status = null,
        [FromQuery(Name = "hub_id")] Guid? hubId = null,
        CancellationToken ct = default)
    {
        if (hubId is { } scopedHubId)
        {
            var denied = await CheckHubAccessAsync(scopedHubId, ct);
            if (denied is not null)
                return denied.ToActionResult();
        }
        else if (IsHubStaff())
        {
            return BadRequest(ApiResponse.Err("HUB_ID_REQUIRED", "hub_id is required for hub staff."));
        }

        var result = await sender.Send(
            new ListRoutesQuery(cursor, pageSize, serviceDate, status, hubId), ct);
        return result.IsSuccess
            ? Ok(ApiResponse.OkPaged(result.Value.Items, result.Value.PageSize, result.Value.NextCursor))
            : result.Error.ToActionResult();
    }

    [HttpGet("suggestions")]
    [Authorize(Roles = "admin,operations_manager")]
    public async Task<IActionResult> GetSuggestionsAsync(
        [FromQuery(Name = "service_date")] DateOnly serviceDate,
        [FromQuery(Name = "include_batched")] bool includeBatched = false,
        CancellationToken ct = default)
    {
        var result = await sender.Send(
            new GetRouteSuggestionsQuery(serviceDate, includeBatched), ct);
        return result.IsSuccess ? Ok(ApiResponse.Ok(result.Value)) : result.Error.ToActionResult();
    }

    [HttpGet("{routeId:guid}/eligibility")]
    [Authorize(Roles = "admin,operations_manager,hub_staff")]
    public async Task<IActionResult> CheckEligibilityAsync(
        Guid routeId,
        [FromQuery] Guid vehicleId,
        [FromQuery(Name = "driver_user_id")] Guid driverUserId,
        CancellationToken ct)
    {
        var denied = await CheckRouteAccessAsync(routeId, ct);
        if (denied is not null)
            return denied.ToActionResult();

        var result = await sender.Send(new CheckEligibilityQuery(routeId, vehicleId, driverUserId), ct);
        return result.IsSuccess ? Ok(ApiResponse.Ok(result.Value)) : result.Error.ToActionResult();
    }

    [HttpGet("{id:guid}")]
    [Authorize(Roles = "admin,operations_manager,hub_staff")]
    public async Task<IActionResult> GetRouteAsync(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new GetRouteQuery(id), ct);
        if (result.IsSuccess)
        {
            var denied = await CheckHubAccessAsync(result.Value.HubId, ct);
            if (denied is not null)
                return denied.ToActionResult();
        }

        return result.IsSuccess ? Ok(ApiResponse.Ok(result.Value)) : result.Error.ToActionResult();
    }

    [HttpGet("{routeId:guid}/deliveries")]
    [Authorize(Roles = "admin,operations_manager")]
    public async Task<IActionResult> GetRouteDeliveriesAsync(Guid routeId, CancellationToken ct)
    {
        var result = await sender.Send(new GetRouteDeliveriesQuery(routeId), ct);
        return result.IsSuccess ? Ok(ApiResponse.Ok(result.Value)) : result.Error.ToActionResult();
    }

    // What to load onto the truck per restaurant stop, in loading order
    // (furthest/last-delivered first). Goods only -- no prices or credit.
    [HttpGet("{id:guid}/loading-manifest")]
    [Authorize(Roles = "admin,operations_manager,hub_staff,driver")]
    public async Task<IActionResult> GetLoadingManifestAsync(Guid id, CancellationToken ct)
    {
        var denied = await CheckRouteAccessAsync(id, ct);
        if (denied is not null)
            return denied.ToActionResult();

        var result = await sender.Send(new GetLoadingManifestQuery(id), ct);
        return result.IsSuccess ? Ok(ApiResponse.Ok(result.Value)) : result.Error.ToActionResult();
    }

    private async Task<FreshFlow.SharedKernel.Application.Error?> CheckRouteAccessAsync(
        Guid routeId,
        CancellationToken ct)
    {
        var isDriver = HttpContext?.User.IsInRole("driver") == true;
        if (!IsHubStaff() && !isDriver)
            return null;

        var result = await sender.Send(new GetRouteQuery(routeId), ct);
        if (!result.IsSuccess)
            return result.Error;

        if (!isDriver)
            return await CheckHubAccessAsync(result.Value.HubId, ct);

        var rawUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(rawUserId, out var driverUserId) && result.Value.DriverUserId == driverUserId
            ? null
            : FreshFlow.SharedKernel.Application.Error.Unauthorized(
                "FORBIDDEN", "This route is not assigned to the authenticated driver.");
    }

    private async Task<FreshFlow.SharedKernel.Application.Error?> CheckHubAccessAsync(
        Guid? hubId,
        CancellationToken ct)
    {
        if (!IsHubStaff())
            return null;

        if (hubId is null)
            return FreshFlow.SharedKernel.Application.Error.Unauthorized(
                "HUB_ACCESS_DENIED", "Route is not assigned to a hub.");

        var hubs = HttpContext.RequestServices.GetRequiredService<IHubRepository>();
        var hubAccess = HttpContext.RequestServices.GetRequiredService<HubAccessChecker>();
        var hub = await hubs.FindByIdAsync(hubId.Value, ct);
        if (hub is null)
            return FreshFlow.SharedKernel.Application.Error.NotFound("HUB", hubId.Value);

        var rawUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(rawUserId, out var actorUserId)
            ? await hubAccess.CheckAsync(
                hub.Id,
                hub.IsActive,
                actorUserId,
                User.IsInRole("admin") || User.IsInRole("operations_manager"),
                ct)
            : FreshFlow.SharedKernel.Application.Error.Unauthorized(
                "HUB_ACCESS_DENIED", "You do not have access to this hub.");
    }

    private bool IsHubStaff() => HttpContext?.User.IsInRole("hub_staff") == true;
}

public sealed record CalculateRouteRequest(
    Guid HubId,
    IReadOnlyList<Guid> DestinationRestaurantIds,
    string? OptimizationCriteria,
    DateOnly ServiceDate);

public sealed record PlanRoutesRequest(
    Guid HubId,
    DateOnly ServiceDate,
    string? OptimizationCriteria);

public sealed record OptimizeRouteRequest(string OptimizationCriteria);

public sealed record ReviewRouteRequest(IReadOnlyList<Guid>? StopOrder);

public sealed record AssignVehicleRequest(Guid VehicleId, Guid DriverUserId);
