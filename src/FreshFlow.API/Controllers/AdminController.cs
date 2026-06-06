using System.Security.Claims;
using FreshFlow.API.Extensions;
using FreshFlow.Auth.Application.Commands.Admin.ActivateUser;
using FreshFlow.Auth.Application.Commands.Admin.ApproveRestaurant;
using FreshFlow.Auth.Application.Commands.Admin.AssignRole;
using FreshFlow.Auth.Application.Commands.Admin.CreateUser;
using FreshFlow.Auth.Application.Commands.Admin.UnlockUser;
using FreshFlow.Auth.Application.Queries.GetRoles;
using FreshFlow.Auth.Application.Queries.GetUsers;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FreshFlow.API.Controllers;

[ApiController]
[Route("api/v1/admin")]
[Authorize(Roles = "admin")]
public sealed class AdminController(ISender sender) : ControllerBase
{
    [HttpPost("users")]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserCommand command, CancellationToken ct)
    {
        var result = await sender.Send(command, ct);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetUsers), null, result.Value)
            : result.Error.ToActionResult();
    }

    [HttpGet("users")]
    public async Task<IActionResult> GetUsers(
        [FromQuery] string? role,
        [FromQuery] bool? isActive,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await sender.Send(new GetUsersQuery(role, isActive, search, page, pageSize), ct);
        return result.IsSuccess ? Ok(result.Value) : result.Error.ToActionResult();
    }

    [HttpPatch("users/{userId:guid}/activate")]
    public async Task<IActionResult> ActivateUser(Guid userId, [FromBody] ActivateRequest body, CancellationToken ct)
    {
        var adminId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
                                 ?? User.FindFirstValue("sub")
                                 ?? throw new UnauthorizedAccessException());

        var result = await sender.Send(new ActivateUserCommand(userId, body.IsActive, adminId), ct);
        return result.IsSuccess ? Ok(result.Value) : result.Error.ToActionResult();
    }

    [HttpPost("users/{userId:guid}/unlock")]
    public async Task<IActionResult> UnlockUser(Guid userId, CancellationToken ct)
    {
        var result = await sender.Send(new UnlockUserCommand(userId), ct);
        return result.IsSuccess ? NoContent() : result.Error.ToActionResult();
    }

    [HttpGet("roles")]
    public async Task<IActionResult> GetRoles(CancellationToken ct)
    {
        var result = await sender.Send(new GetRolesQuery(), ct);
        return result.IsSuccess ? Ok(result.Value) : result.Error.ToActionResult();
    }

    [HttpPatch("users/{userId:guid}/role")]
    public async Task<IActionResult> AssignRole(
        Guid userId, [FromBody] AssignRoleRequest body, CancellationToken ct)
    {
        var result = await sender.Send(new AssignRoleCommand(userId, body.RoleName), ct);
        return result.IsSuccess ? Ok(result.Value) : result.Error.ToActionResult();
    }

    [HttpPatch("restaurants/{restaurantId:guid}/approve")]
    public async Task<IActionResult> ApproveRestaurant(Guid restaurantId, CancellationToken ct)
    {
        var result = await sender.Send(new ApproveRestaurantCommand(restaurantId), ct);
        return result.IsSuccess ? Ok(result.Value) : result.Error.ToActionResult();
    }
}

public sealed record ActivateRequest(bool IsActive);
public sealed record AssignRoleRequest(string RoleName);
