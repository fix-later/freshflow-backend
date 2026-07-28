using System.Security.Claims;
using FreshFlow.API.Extensions;
using FreshFlow.Hub.Application.Commands.AcknowledgeDiscrepancy;
using FreshFlow.Hub.Application.Commands.CreateCrossDock;
using FreshFlow.Hub.Application.Commands.MarkLineSorted;
using FreshFlow.Hub.Application.Commands.RecordDiscrepancy;
using FreshFlow.Hub.Application.Commands.RecordInbound;
using FreshFlow.Hub.Application.Commands.RecordOutbound;
using FreshFlow.Hub.Application.Commands.ScanInbound;
using FreshFlow.Hub.Application.Queries.GetHubOrdersByRestaurant;
using FreshFlow.Hub.Application.Queries.GetHubProcurementPlan;
using FreshFlow.Hub.Application.Queries.GetPendingInbound;
using FreshFlow.Hub.Application.Queries.GetSortingProgress;
using FreshFlow.Hub.Application.Queries.ListCrossDock;
using FreshFlow.Hub.Application.Queries.ListDiscrepancies;
using FreshFlow.Hub.Application.Queries.ListInbound;
using FreshFlow.Hub.Application.Queries.ListOutbound;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FreshFlow.API.Controllers;

[ApiController]
[Route("api/v1/hubs")]
[Authorize(Roles = "hub_staff,admin,operations_manager")]
public sealed class HubInboundController(ISender sender) : ControllerBase
{
    [HttpPost("{hubId:guid}/inbound")]
    public async Task<IActionResult> RecordInboundAsync(
        Guid hubId,
        [FromBody] RecordInboundRequest body,
        CancellationToken ct)
    {
        var result = await sender.Send(
            new RecordInboundCommand(
                hubId,
                body.SourceMarketId,
                body.DeliveryScheduleId,
                body.Items.Select(item => new HubInboundItemCommand(
                    item.MarketProductId,
                    item.ProductId,
                    item.QuantityKg)).ToList().AsReadOnly(),
                body.ArrivedAt,
                ResolveUserId(),
                BypassHubAssignment()),
            ct);

        return result.IsSuccess
            ? Created($"/api/v1/hubs/{hubId}/inbound/{result.Value.InboundId}", ApiResponse.Ok(result.Value))
            : result.Error.ToActionResult();
    }

    [HttpPost("scan")]
    public async Task<IActionResult> ScanInboundAsync(
        [FromBody] ScanInboundRequest body,
        CancellationToken ct)
    {
        var result = await sender.Send(
            new ScanInboundCommand(body.Code, ResolveUserId(), BypassHubAssignment()),
            ct);
        return result.IsSuccess ? Ok(ApiResponse.Ok(result.Value)) : result.Error.ToActionResult();
    }

    [HttpGet("{hubId:guid}/pending-inbound")]
    public async Task<IActionResult> GetPendingInboundAsync(
        Guid hubId,
        [FromQuery] string? cursor = null,
        [FromQuery(Name = "page_size")] int pageSize = 50,
        CancellationToken ct = default)
    {
        var result = await sender.Send(
            new GetPendingInboundQuery(
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

    [HttpGet("{hubId:guid}/procurement-plan")]
    public async Task<IActionResult> GetProcurementPlanAsync(
        Guid hubId,
        [FromQuery] DateOnly date,
        CancellationToken ct)
    {
        var result = await sender.Send(
            new GetHubProcurementPlanQuery(
                hubId,
                date,
                ResolveUserId(),
                BypassHubAssignment()),
            ct);
        return result.IsSuccess ? Ok(ApiResponse.Ok(result.Value)) : result.Error.ToActionResult();
    }

    [HttpGet("{hubId:guid}/orders-by-restaurant")]
    public async Task<IActionResult> GetOrdersByRestaurantAsync(
        Guid hubId,
        [FromQuery(Name = "service_date")] DateOnly serviceDate,
        [FromQuery(Name = "include_batched")] bool includeBatched = false,
        CancellationToken ct = default)
    {
        var result = await sender.Send(
            new GetHubOrdersByRestaurantQuery(hubId, serviceDate, includeBatched),
            ct);
        return result.IsSuccess ? Ok(ApiResponse.Ok(result.Value)) : result.Error.ToActionResult();
    }

    [HttpGet("{hubId:guid}/inbound")]
    public async Task<IActionResult> ListInboundAsync(
        Guid hubId,
        [FromQuery] DateOnly? date = null,
        [FromQuery] string? cursor = null,
        [FromQuery(Name = "page_size")] int pageSize = 50,
        CancellationToken ct = default)
    {
        var result = await sender.Send(
            new ListInboundQuery(
                hubId,
                date,
                cursor,
                pageSize,
                ResolveUserId(),
                BypassHubAssignment()),
            ct);
        return result.IsSuccess ? Ok(ApiResponse.Ok(result.Value)) : result.Error.ToActionResult();
    }

    [HttpPost("{hubId:guid}/inbound/{inboundId:guid}/discrepancy")]
    public async Task<IActionResult> RecordDiscrepancyAsync(
        Guid hubId,
        Guid inboundId,
        [FromBody] RecordDiscrepancyRequest body,
        CancellationToken ct)
    {
        var result = await sender.Send(
            new RecordDiscrepancyCommand(
                hubId,
                inboundId,
                body.OrderItemId,
                body.AffectedQuantity,
                body.ConditionStatus,
                body.Notes,
                ResolveUserId(),
                BypassHubAssignment()),
            ct);

        return result.IsSuccess
            ? Created(
                $"/api/v1/hubs/{hubId}/discrepancies/{result.Value.DiscrepancyId}",
                ApiResponse.Ok(result.Value))
            : result.Error.ToActionResult();
    }

    [HttpGet("{hubId:guid}/discrepancies")]
    public async Task<IActionResult> ListDiscrepanciesAsync(
        Guid hubId,
        [FromQuery] string? status = null,
        [FromQuery] string? cursor = null,
        [FromQuery(Name = "page_size")] int pageSize = 50,
        CancellationToken ct = default)
    {
        var result = await sender.Send(
            new ListDiscrepanciesQuery(
                hubId,
                status,
                cursor,
                pageSize,
                ResolveUserId(),
                BypassHubAssignment()),
            ct);
        return result.IsSuccess
            ? Ok(ApiResponse.OkPaged(result.Value.Items, result.Value.PageSize, result.Value.NextCursor))
            : result.Error.ToActionResult();
    }

    [HttpPost("{hubId:guid}/discrepancies/{discrepancyId:guid}/acknowledge")]
    [Authorize(Roles = "admin,operations_manager")]
    public async Task<IActionResult> AcknowledgeDiscrepancyAsync(
        Guid hubId,
        Guid discrepancyId,
        CancellationToken ct)
    {
        var result = await sender.Send(
            new AcknowledgeDiscrepancyCommand(hubId, discrepancyId, ResolveUserId()),
            ct);

        return result.IsSuccess ? Ok(ApiResponse.Ok(result.Value)) : result.Error.ToActionResult();
    }

    [HttpPost("{hubId:guid}/cross-dock")]
    public async Task<IActionResult> CreateCrossDockAsync(
        Guid hubId,
        [FromBody] CreateCrossDockRequest body,
        CancellationToken ct)
    {
        var result = await sender.Send(
            new CreateCrossDockCommand(
                hubId,
                body.InboundEventId,
                body.OutboundRouteId,
                body.Notes,
                ResolveUserId(),
                BypassHubAssignment()),
            ct);

        return result.IsSuccess
            ? Created($"/api/v1/hubs/{hubId}/cross-dock/{result.Value.CrossDockId}", ApiResponse.Ok(result.Value))
            : result.Error.ToActionResult();
    }

    [HttpGet("{hubId:guid}/cross-dock")]
    public async Task<IActionResult> ListCrossDockAsync(
        Guid hubId,
        [FromQuery] string? status = null,
        [FromQuery] string? cursor = null,
        [FromQuery(Name = "page_size")] int pageSize = 50,
        CancellationToken ct = default)
    {
        var result = await sender.Send(
            new ListCrossDockQuery(
                hubId,
                status,
                cursor,
                pageSize,
                ResolveUserId(),
                BypassHubAssignment()),
            ct);
        return result.IsSuccess
            ? Ok(ApiResponse.OkPaged(result.Value.Items, result.Value.PageSize, result.Value.NextCursor))
            : result.Error.ToActionResult();
    }

    [HttpPost("{hubId:guid}/outbound")]
    public async Task<IActionResult> RecordOutboundAsync(
        Guid hubId,
        [FromBody] RecordOutboundRequest body,
        CancellationToken ct)
    {
        var result = await sender.Send(
            new RecordOutboundCommand(
                hubId,
                body.DestinationRouteId,
                body.Items.Select(item => new HubOutboundItemCommand(
                    item.MarketProductId,
                    item.ProductId,
                    item.QuantityKg)).ToList().AsReadOnly(),
                body.DispatchedAt,
                ResolveUserId(),
                BypassHubAssignment()),
            ct);

        return result.IsSuccess
            ? Created($"/api/v1/hubs/{hubId}/outbound/{result.Value.OutboundId}", ApiResponse.Ok(result.Value))
            : result.Error.ToActionResult();
    }

    [HttpGet("{hubId:guid}/outbound")]
    public async Task<IActionResult> ListOutboundAsync(
        Guid hubId,
        [FromQuery] DateOnly? date = null,
        [FromQuery] string? cursor = null,
        [FromQuery(Name = "page_size")] int pageSize = 50,
        CancellationToken ct = default)
    {
        var result = await sender.Send(
            new ListOutboundQuery(
                hubId,
                date,
                cursor,
                pageSize,
                ResolveUserId(),
                BypassHubAssignment()),
            ct);
        return result.IsSuccess ? Ok(ApiResponse.Ok(result.Value)) : result.Error.ToActionResult();
    }

    [HttpPost("{hubId:guid}/routes/{routeId:guid}/sorting")]
    public async Task<IActionResult> MarkLineSortedAsync(
        Guid hubId,
        Guid routeId,
        [FromBody] MarkLineSortedRequest body,
        CancellationToken ct)
    {
        var result = await sender.Send(
            new MarkLineSortedCommand(
                hubId,
                routeId,
                body.OrderItemId,
                body.SortedQuantityKg,
                ResolveUserId(),
                BypassHubAssignment()),
            ct);

        return result.IsSuccess ? Ok(ApiResponse.Ok(result.Value)) : result.Error.ToActionResult();
    }

    [HttpGet("{hubId:guid}/routes/{routeId:guid}/sorting-progress")]
    public async Task<IActionResult> GetSortingProgressAsync(
        Guid hubId,
        Guid routeId,
        CancellationToken ct)
    {
        var result = await sender.Send(
            new GetSortingProgressQuery(hubId, routeId, ResolveUserId(), BypassHubAssignment()),
            ct);
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

public sealed record RecordInboundRequest(
    Guid? SourceMarketId,
    Guid? DeliveryScheduleId,
    IReadOnlyList<RecordInboundItemRequest> Items,
    DateTime ArrivedAt);

public sealed record RecordInboundItemRequest(
    Guid MarketProductId,
    Guid? ProductId,
    decimal QuantityKg);

public sealed record ScanInboundRequest(string Code);

public sealed record RecordDiscrepancyRequest(
    Guid OrderItemId,
    decimal AffectedQuantity,
    string ConditionStatus,
    string? Notes);

public sealed record CreateCrossDockRequest(
    Guid InboundEventId,
    Guid OutboundRouteId,
    string? Notes);

public sealed record RecordOutboundRequest(
    Guid DestinationRouteId,
    IReadOnlyList<RecordOutboundItemRequest> Items,
    DateTime DispatchedAt);

public sealed record RecordOutboundItemRequest(
    Guid MarketProductId,
    Guid? ProductId,
    decimal QuantityKg);

public sealed record MarkLineSortedRequest(Guid OrderItemId, decimal SortedQuantityKg);
