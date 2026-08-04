using System.Security.Claims;
using FreshFlow.API.Extensions;
using FreshFlow.Orders.Application.Commands.ApproveClaim;
using FreshFlow.Orders.Application.Commands.FileClaim;
using FreshFlow.Orders.Application.Commands.RejectClaim;
using FreshFlow.Orders.Application.Queries.GetClaimById;
using FreshFlow.Orders.Application.Queries.ListClaims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FreshFlow.API.Controllers;

[ApiController]
[Route("api/v1/claims")]
[Authorize(Roles = "admin,operations_manager,restaurant")]
public sealed class ClaimsController(ISender sender) : ControllerBase
{
    [HttpPost("/api/v1/orders/{orderId:guid}/claims")]
    [Authorize(Roles = "restaurant")]
    public async Task<IActionResult> FileAsync(
        Guid orderId,
        [FromBody] FileClaimRequest body,
        CancellationToken ct)
    {
        var result = await sender.Send(
            new FileClaimCommand(ResolveUserId(), orderId, body.Amount, body.Reason),
            ct);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetAsync), new { claimId = result.Value.ClaimId }, ApiResponse.Ok(result.Value))
            : result.Error.ToActionResult();
    }

    [HttpPatch("{claimId:guid}/approve")]
    [Authorize(Roles = "admin,operations_manager")]
    public async Task<IActionResult> ApproveAsync(
        Guid claimId,
        [FromBody] ApproveClaimRequest? body,
        CancellationToken ct)
    {
        var result = await sender.Send(
            new ApproveClaimCommand(ResolveUserId(), claimId, body?.DecisionNote),
            ct);

        return result.IsSuccess
            ? Ok(ApiResponse.Ok(result.Value))
            : result.Error.ToActionResult();
    }

    [HttpPatch("{claimId:guid}/reject")]
    [Authorize(Roles = "admin,operations_manager")]
    public async Task<IActionResult> RejectAsync(
        Guid claimId,
        [FromBody] RejectClaimRequest body,
        CancellationToken ct)
    {
        var result = await sender.Send(
            new RejectClaimCommand(ResolveUserId(), claimId, body.DecisionNote),
            ct);

        return result.IsSuccess
            ? Ok(ApiResponse.Ok(result.Value))
            : result.Error.ToActionResult();
    }

    [HttpGet("{claimId:guid}")]
    public async Task<IActionResult> GetAsync(Guid claimId, CancellationToken ct)
    {
        var result = await sender.Send(
            new GetClaimByIdQuery(ResolveUserId(), IsPrivileged(), claimId),
            ct);

        return result.IsSuccess
            ? Ok(ApiResponse.Ok(result.Value))
            : result.Error.ToActionResult();
    }

    [HttpGet]
    public async Task<IActionResult> ListAsync(
        [FromQuery] Guid? restaurantId,
        [FromQuery] string? status,
        [FromQuery] string? cursor,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        var result = await sender.Send(
            new ListClaimsQuery(
                ResolveUserId(),
                IsPrivileged(),
                restaurantId,
                status,
                cursor,
                pageSize),
            ct);

        return result.IsSuccess
            ? Ok(ApiResponse.OkPaged(result.Value.Items, result.Value.PageSize, result.Value.NextCursor))
            : result.Error.ToActionResult();
    }

    private bool IsPrivileged() =>
        User.IsInRole("admin") || User.IsInRole("operations_manager");

    private Guid ResolveUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(raw, out var id)
            ? id
            : throw new UnauthorizedAccessException("User ID claim is missing or malformed.");
    }
}

public sealed record FileClaimRequest(decimal Amount, string Reason);
public sealed record ApproveClaimRequest(string? DecisionNote);
public sealed record RejectClaimRequest(string DecisionNote);
