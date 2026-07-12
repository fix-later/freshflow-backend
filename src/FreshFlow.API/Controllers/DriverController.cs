using System.Security.Claims;
using FreshFlow.API.Extensions;
using FreshFlow.Logistics.Application.Commands.AttachProofOfDelivery;
using FreshFlow.Logistics.Application.Commands.ConfirmPickup;
using FreshFlow.Logistics.Application.Commands.CreateProofUploadSignature;
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

    private Guid ResolveUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? User.FindFirstValue("sub");
        return Guid.Parse(raw!);
    }
}

public sealed record ConfirmPickupRequest(IReadOnlyList<Guid> OrderIds);

public sealed record AttachProofOfDeliveryRequest(string ProofUrl);
