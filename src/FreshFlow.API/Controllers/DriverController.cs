using System.Security.Claims;
using FreshFlow.API.Extensions;
using FreshFlow.Logistics.Application.Commands.AttachProofOfDelivery;
using FreshFlow.Logistics.Application.Commands.ConfirmPickup;
using FreshFlow.Logistics.Application.Commands.CreateProofUploadSignature;
using FreshFlow.Logistics.Application.Commands.ReorderDriverRoute;
using FreshFlow.Logistics.Application.Commands.ReportDeliveryIssue;
using FreshFlow.Logistics.Application.Commands.StartRoute;
using FreshFlow.Logistics.Application.Commands.UpdateDeliveryStatus;
using FreshFlow.Logistics.Application.Queries.GetDriverRoutesToday;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FreshFlow.API.Controllers;

[ApiController]
[Route("api/v1/driver")]
[Authorize(Roles = "driver")]
public sealed class DriverController(ISender sender) : ControllerBase
{
    [HttpGet("routes/today")]
    public async Task<IActionResult> GetRoutesTodayAsync(CancellationToken ct)
    {
        var result = await sender.Send(new GetDriverRoutesTodayQuery(ResolveUserId()), ct);
        return result.IsSuccess ? Ok(ApiResponse.Ok(result.Value)) : result.Error.ToActionResult();
    }

    [HttpPost("routes/{routeId:guid}/start")]
    public async Task<IActionResult> StartRouteAsync(Guid routeId, CancellationToken ct)
    {
        var result = await sender.Send(new StartRouteCommand(routeId, ResolveUserId()), ct);
        return result.IsSuccess ? Ok(ApiResponse.Ok(result.Value)) : result.Error.ToActionResult();
    }

    [HttpPost("routes/{routeId:guid}/reorder")]
    public async Task<IActionResult> ReorderRouteAsync(
        Guid routeId,
        [FromBody] ReorderRouteRequest body,
        CancellationToken ct)
    {
        var result = await sender.Send(
            new ReorderDriverRouteCommand(routeId, ResolveUserId(), body.StopOrder), ct);
        return result.IsSuccess ? Ok(ApiResponse.Ok(result.Value)) : result.Error.ToActionResult();
    }

    [HttpPost("routes/{routeId:guid}/confirm-pickup")]
    public async Task<IActionResult> ConfirmPickupAsync(
        Guid routeId,
        [FromBody] ConfirmPickupRequest body,
        CancellationToken ct)
    {
        var result = await sender.Send(new ConfirmPickupCommand(routeId, ResolveUserId(), body.OrderIds), ct);
        return result.IsSuccess
            ? Created($"/api/v1/driver/routes/{routeId}/confirm-pickup", ApiResponse.Ok(result.Value))
            : result.Error.ToActionResult();
    }

    [HttpPost("deliveries/{deliveryId:guid}/proof-of-delivery/upload-signature")]
    public async Task<IActionResult> CreateProofUploadSignatureAsync(Guid deliveryId, CancellationToken ct)
    {
        var result = await sender.Send(new CreateProofUploadSignatureCommand(deliveryId, ResolveUserId()), ct);
        return result.IsSuccess ? Ok(ApiResponse.Ok(result.Value)) : result.Error.ToActionResult();
    }

    [HttpPut("deliveries/{deliveryId:guid}/proof-of-delivery")]
    public async Task<IActionResult> AttachProofOfDeliveryAsync(
        Guid deliveryId,
        [FromBody] AttachProofOfDeliveryRequest body,
        CancellationToken ct)
    {
        var result = await sender.Send(
            new AttachProofOfDeliveryCommand(deliveryId, ResolveUserId(), body.ProofUrl),
            ct);

        return result.IsSuccess ? Ok(ApiResponse.Ok(result.Value)) : result.Error.ToActionResult();
    }

    [HttpPatch("deliveries/{deliveryId:guid}/status")]
    public async Task<IActionResult> UpdateDeliveryStatusAsync(
        Guid deliveryId,
        [FromBody] UpdateDeliveryStatusRequest body,
        CancellationToken ct)
    {
        var result = await sender.Send(
            new UpdateDeliveryStatusCommand(deliveryId, ResolveUserId(), body.Status, body.FailureReason),
            ct);

        return result.IsSuccess ? Ok(ApiResponse.Ok(result.Value)) : result.Error.ToActionResult();
    }

    [HttpPost("deliveries/{deliveryId:guid}/issues")]
    public async Task<IActionResult> ReportDeliveryIssueAsync(
        Guid deliveryId,
        [FromBody] ReportDeliveryIssueRequest body,
        CancellationToken ct)
    {
        var result = await sender.Send(
            new ReportDeliveryIssueCommand(deliveryId, ResolveUserId(), body.IssueType, body.Description),
            ct);

        return result.IsSuccess
            ? Created(
                $"/api/v1/driver/deliveries/{deliveryId}/issues/{result.Value.Id}",
                ApiResponse.Ok(result.Value))
            : result.Error.ToActionResult();
    }

    private Guid ResolveUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? User.FindFirstValue("sub");
        return Guid.Parse(raw!);
    }
}

public sealed record ConfirmPickupRequest(IReadOnlyList<Guid> OrderIds);

public sealed record AttachProofOfDeliveryRequest(string ProofUrl);

public sealed record UpdateDeliveryStatusRequest(string Status, string? FailureReason);

public sealed record ReorderRouteRequest(IReadOnlyList<Guid> StopOrder);
