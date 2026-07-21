using System.Security.Claims;
using FreshFlow.API.Extensions;
using FreshFlow.Hub.Application.Commands.ReplaceHubStaffAssignments;
using FreshFlow.Hub.Application.Queries.GetAssignedHubs;
using FreshFlow.Hub.Application.Queries.GetHubStaffAssignments;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FreshFlow.API.Controllers;

[ApiController]
[Route("api/v1/hubs")]
public sealed class HubStaffAssignmentsController(ISender sender) : ControllerBase
{
    [HttpGet("{hubId:guid}/staff-assignments")]
    [Authorize(Roles = "admin,operations_manager")]
    public async Task<IActionResult> GetAssignmentsAsync(
        Guid hubId,
        CancellationToken ct)
    {
        var result = await sender.Send(
            new GetHubStaffAssignmentsQuery(hubId, ResolveUserId()), ct);
        return result.IsSuccess ? Ok(ApiResponse.Ok(result.Value)) : result.Error.ToActionResult();
    }

    [HttpPut("{hubId:guid}/staff-assignments")]
    [Authorize(Roles = "admin,operations_manager")]
    public async Task<IActionResult> ReplaceAssignmentsAsync(
        Guid hubId,
        [FromBody] ReplaceHubStaffAssignmentsRequest body,
        CancellationToken ct)
    {
        var result = await sender.Send(
            new ReplaceHubStaffAssignmentsCommand(hubId, body.StaffUserIds, ResolveUserId()),
            ct);

        return result.IsSuccess ? Ok(ApiResponse.Ok(result.Value)) : result.Error.ToActionResult();
    }

    [HttpGet("assigned")]
    [Authorize(Roles = "hub_staff")]
    public async Task<IActionResult> GetAssignedAsync(CancellationToken ct)
    {
        var result = await sender.Send(new GetAssignedHubsQuery(ResolveUserId()), ct);
        return result.IsSuccess ? Ok(ApiResponse.Ok(result.Value)) : result.Error.ToActionResult();
    }

    private Guid ResolveUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? User.FindFirstValue("sub");
        return Guid.Parse(raw!);
    }
}

public sealed record ReplaceHubStaffAssignmentsRequest(IReadOnlyList<Guid> StaffUserIds);
