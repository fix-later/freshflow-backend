using System.Security.Claims;
using FreshFlow.API.Extensions;
using FreshFlow.Auth.Application.Commands.Admin.ActivateUser;
using FreshFlow.Auth.Application.Commands.Admin.ApproveRestaurant;
using FreshFlow.Auth.Application.Commands.Admin.AssignRole;
using FreshFlow.Auth.Application.Commands.Admin.CreateUser;
using FreshFlow.Auth.Application.Commands.Admin.ReplaceMarketAssignments;
using FreshFlow.Auth.Application.Commands.Admin.UnlockUser;
using FreshFlow.Auth.Application.Queries.GetMarketAssignments;
using FreshFlow.Auth.Application.Queries.GetRoles;
using FreshFlow.Auth.Application.Queries.GetUsers;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FreshFlow.API.Controllers;

[ApiController]
[Route("api/v1/admin")]
[Authorize]   // All endpoints require authentication; roles are enforced per-action below.
public sealed class AdminController(ISender sender) : ControllerBase
{
    [HttpPost("users")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> CreateUserAsync([FromBody] CreateUserCommand command, CancellationToken ct)
    {
        var result = await sender.Send(command, ct);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetUsersAsync), null, ApiResponse.Ok(result.Value))
            : result.Error.ToActionResult();
    }

    [HttpGet("users")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> GetUsersAsync(
        [FromQuery] string? role,
        [FromQuery] bool? isActive,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await sender.Send(new GetUsersQuery(role, isActive, search, page, pageSize), ct);
        return result.IsSuccess ? Ok(ApiResponse.Ok(result.Value)) : result.Error.ToActionResult();
    }

    [HttpPatch("users/{userId:guid}/activate")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> ActivateUserAsync(Guid userId, [FromBody] ActivateRequest body, CancellationToken ct)
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (!Guid.TryParse(raw, out var adminId))
            return Unauthorized();

        var result = await sender.Send(new ActivateUserCommand(userId, body.IsActive, adminId), ct);
        return result.IsSuccess ? Ok(ApiResponse.Ok(result.Value)) : result.Error.ToActionResult();
    }

    [HttpPost("users/{userId:guid}/unlock")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> UnlockUserAsync(Guid userId, CancellationToken ct)
    {
        var result = await sender.Send(new UnlockUserCommand(userId), ct);
        return result.IsSuccess ? NoContent() : result.Error.ToActionResult();
    }

    [HttpGet("roles")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> GetRolesAsync(CancellationToken ct)
    {
        var result = await sender.Send(new GetRolesQuery(), ct);
        return result.IsSuccess ? Ok(ApiResponse.Ok(result.Value)) : result.Error.ToActionResult();
    }

    [HttpPatch("users/{userId:guid}/role")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> AssignRoleAsync(
        Guid userId, [FromBody] AssignRoleRequest body, CancellationToken ct)
    {
        var result = await sender.Send(new AssignRoleCommand(userId, body.RoleName), ct);
        return result.IsSuccess ? Ok(ApiResponse.Ok(result.Value)) : result.Error.ToActionResult();
    }

    [HttpPatch("restaurants/{restaurantId:guid}/approve")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> ApproveRestaurantAsync(Guid restaurantId, CancellationToken ct)
    {
        var result = await sender.Send(new ApproveRestaurantCommand(restaurantId), ct);
        return result.IsSuccess ? Ok(ApiResponse.Ok(result.Value)) : result.Error.ToActionResult();
    }

    // ── Market Assignments (Admin + Operations Manager) ───────────────────────

    /// <summary>
    /// GET /api/v1/admin/users/{userId}/market-assignments
    /// Returns the current market assignments for the specified user.
    /// </summary>
    [HttpGet("users/{userId:guid}/market-assignments")]
    [Authorize(Roles = "admin,operations_manager")]
    public async Task<IActionResult> GetMarketAssignmentsAsync(Guid userId, CancellationToken ct)
    {
        var result = await sender.Send(new GetMarketAssignmentsQuery(userId), ct);
        return result.IsSuccess ? Ok(ApiResponse.Ok(result.Value)) : result.Error.ToActionResult();
    }

    /// <summary>
    /// PUT /api/v1/admin/users/{userId}/market-assignments
    /// Replaces all market assignments for the specified user (must be a market_agent).
    /// An empty MarketIds array clears all assignments.
    /// </summary>
    [HttpPut("users/{userId:guid}/market-assignments")]
    [Authorize(Roles = "admin,operations_manager")]
    public async Task<IActionResult> ReplaceMarketAssignmentsAsync(
        Guid userId,
        [FromBody] ReplaceMarketAssignmentsRequest body,
        CancellationToken ct)
    {
        var assignedById = Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsedAssignedBy)
            ? parsedAssignedBy
            : (Guid?)null;

        var result = await sender.Send(
            new ReplaceMarketAssignmentsCommand(userId, body.MarketIds, assignedById), ct);

        return result.IsSuccess ? Ok(ApiResponse.Ok(result.Value)) : result.Error.ToActionResult();
    }
}

// ── Request DTOs ──────────────────────────────────────────────────────────────

public sealed record ActivateRequest(bool IsActive);
public sealed record AssignRoleRequest(string RoleName);
public sealed record ReplaceMarketAssignmentsRequest(IReadOnlyList<Guid> MarketIds);
