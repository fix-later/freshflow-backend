using System.Security.Claims;
using FreshFlow.API.Extensions;
using FreshFlow.Hub.Application.Commands.AcknowledgeDiscrepancy;
using FreshFlow.Hub.Application.Commands.RecordDiscrepancy;
using FreshFlow.Hub.Application.Commands.RecordInbound;
using FreshFlow.Hub.Application.Commands.ScanInbound;
using FreshFlow.Hub.Application.Queries.GetPendingInbound;
using FreshFlow.Hub.Application.Queries.ListDiscrepancies;
using FreshFlow.Hub.Application.Queries.ListInbound;
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
                body.ArrivedAt),
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
        var result = await sender.Send(new ScanInboundCommand(body.Code), ct);
        return result.IsSuccess ? Ok(ApiResponse.Ok(result.Value)) : result.Error.ToActionResult();
    }

    [HttpGet("{hubId:guid}/pending-inbound")]
    public async Task<IActionResult> GetPendingInboundAsync(
        Guid hubId,
        [FromQuery] string? cursor = null,
        [FromQuery(Name = "page_size")] int pageSize = 50,
        CancellationToken ct = default)
    {
        var result = await sender.Send(new GetPendingInboundQuery(hubId, cursor, pageSize), ct);
        return result.IsSuccess
            ? Ok(ApiResponse.OkPaged(result.Value.Items, result.Value.PageSize, result.Value.NextCursor))
            : result.Error.ToActionResult();
    }

    [HttpGet("{hubId:guid}/inbound")]
    public async Task<IActionResult> ListInboundAsync(
        Guid hubId,
        [FromQuery] DateOnly? date = null,
        [FromQuery] string? cursor = null,
        [FromQuery(Name = "page_size")] int pageSize = 50,
        CancellationToken ct = default)
    {
        var result = await sender.Send(new ListInboundQuery(hubId, date, cursor, pageSize), ct);
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
                body.Notes),
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
        var result = await sender.Send(new ListDiscrepanciesQuery(hubId, status, cursor, pageSize), ct);
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

    private Guid ResolveUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? User.FindFirstValue("sub");
        return Guid.Parse(raw!);
    }
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
