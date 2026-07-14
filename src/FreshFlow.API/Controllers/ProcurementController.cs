using System.Security.Claims;
using FreshFlow.API.Extensions;
using FreshFlow.Procurement.Application.Commands.ConfirmPurchase;
using FreshFlow.Procurement.Application.Commands.HandoverBatch;
using FreshFlow.Procurement.Application.Queries.GetAssignedProcurementTask;
using FreshFlow.Procurement.Application.Queries.GetAssignedProcurementTasks;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FreshFlow.API.Controllers;

[ApiController]
[Route("api/v1/procurement")]
[Authorize(Roles = "market_agent")]
public sealed class ProcurementController(ISender sender) : ControllerBase
{
    [HttpGet("tasks")]
    public async Task<IActionResult> GetTasksAsync(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        if (!TryResolveUserId(out var agentUserId))
            return Unauthorized();

        var result = await sender.Send(
            new GetAssignedProcurementTasksQuery(agentUserId, page, pageSize),
            ct);
        return result.IsSuccess ? Ok(ApiResponse.Ok(result.Value)) : result.Error.ToActionResult();
    }

    [HttpGet("tasks/{batchId:guid}")]
    public async Task<IActionResult> GetTaskAsync(Guid batchId, CancellationToken ct)
    {
        if (!TryResolveUserId(out var agentUserId))
            return Unauthorized();

        var result = await sender.Send(
            new GetAssignedProcurementTaskQuery(agentUserId, batchId),
            ct);
        return result.IsSuccess ? Ok(ApiResponse.Ok(result.Value)) : result.Error.ToActionResult();
    }

    [HttpPatch("tasks/{batchId:guid}/purchase")]
    public async Task<IActionResult> ConfirmPurchaseAsync(
        Guid batchId,
        [FromBody] ConfirmPurchaseRequest body,
        CancellationToken ct)
    {
        if (!TryResolveUserId(out var agentUserId))
            return Unauthorized();

        var result = await sender.Send(
            new ConfirmPurchaseCommand(batchId, agentUserId, body.Lines),
            ct);
        return result.IsSuccess ? Ok(ApiResponse.Ok(result.Value)) : result.Error.ToActionResult();
    }

    [HttpPatch("tasks/{batchId:guid}/handover")]
    public async Task<IActionResult> HandoverAsync(
        Guid batchId,
        [FromBody] HandoverRequest body,
        CancellationToken ct)
    {
        if (!TryResolveUserId(out var agentUserId))
            return Unauthorized();

        var result = await sender.Send(
            new HandoverBatchCommand(batchId, agentUserId, body.HubId),
            ct);
        return result.IsSuccess ? Ok(ApiResponse.Ok(result.Value)) : result.Error.ToActionResult();
    }

    private bool TryResolveUserId(out Guid userId)
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? User.FindFirstValue("sub");
        return Guid.TryParse(raw, out userId);
    }
}

public sealed record ConfirmPurchaseRequest(IReadOnlyList<PurchaseLineDto> Lines);

public sealed record HandoverRequest(Guid? HubId);
