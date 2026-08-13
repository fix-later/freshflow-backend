using System.Security.Claims;
using FreshFlow.API.Extensions;
using FreshFlow.Auth.Application.Commands.Admin.ActivateUser;
using FreshFlow.Auth.Application.Commands.Admin.ApproveRestaurant;
using FreshFlow.Auth.Application.Commands.Admin.AssignRole;
using FreshFlow.Auth.Application.Commands.Admin.CreateUser;
using FreshFlow.Auth.Application.Commands.Admin.ReactivateRestaurant;
using FreshFlow.Auth.Application.Commands.Admin.ReplaceMarketAssignments;
using FreshFlow.Auth.Application.Commands.Admin.SuspendRestaurant;
using FreshFlow.Auth.Application.Commands.Admin.UnlockUser;
using FreshFlow.Auth.Application.Queries.GetMarketAssignments;
using FreshFlow.Auth.Application.Queries.GetRestaurantProfileById;
using FreshFlow.Auth.Application.Queries.GetRoles;
using FreshFlow.Auth.Application.Queries.GetUsers;
using FreshFlow.Infrastructure.Persistence.Audit;
using FreshFlow.Orders.Application.Commands.SetRestaurantCreditLimit;
using FreshFlow.Orders.Application.Commands.SettleRestaurantCredit;
using FreshFlow.Orders.Application.Commands.UpdateOperationalSettings;
using FreshFlow.Orders.Application.Queries.GetOperationalSettings;
using FreshFlow.Orders.Domain.Enums;
using FreshFlow.Procurement.Application.Commands.AssignBatchItems;
using FreshFlow.Procurement.Application.Commands.CancelBatch;
using FreshFlow.Procurement.Application.Commands.CloseMarketSession;
using FreshFlow.Procurement.Application.Commands.GenerateManifest;
using FreshFlow.Procurement.Application.Commands.OpenMarketSession;
using FreshFlow.Procurement.Application.Commands.ResetBatchingDay;
using FreshFlow.Procurement.Application.Commands.RunAutoBatch;
using FreshFlow.Procurement.Application.Commands.UpdateMarketSession;
using FreshFlow.Procurement.Application.Dtos;
using FreshFlow.Procurement.Application.Queries.GetMarketSession;
using FreshFlow.Procurement.Application.Queries.GetMarketSessions;
using FreshFlow.Procurement.Application.Queries.GetProcurementBatches;
using FreshFlow.Procurement.Application.Queries.GetProcurementProgress;
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
        [FromQuery] string? restaurantStatus = null,
        CancellationToken ct = default)
    {
        var result = await sender.Send(
            new GetUsersQuery(role, isActive, search, page, pageSize, restaurantStatus), ct);
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

    /// <summary>
    /// GET /api/v1/admin/restaurants/{restaurantId}/profile
    /// Returns the full profile (incl. tax/invoice fields) of any restaurant, by its restaurant id.
    /// </summary>
    [HttpGet("restaurants/{restaurantId:guid}/profile")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> GetRestaurantProfileAsync(Guid restaurantId, CancellationToken ct)
    {
        var result = await sender.Send(new GetRestaurantProfileByIdQuery(restaurantId), ct);
        return result.IsSuccess ? Ok(ApiResponse.Ok(result.Value)) : result.Error.ToActionResult();
    }

    [HttpPatch("restaurants/{restaurantId:guid}/approve")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> ApproveRestaurantAsync(Guid restaurantId, CancellationToken ct)
    {
        var result = await sender.Send(new ApproveRestaurantCommand(restaurantId), ct);
        return result.IsSuccess ? Ok(ApiResponse.Ok(result.Value)) : result.Error.ToActionResult();
    }

    [HttpPatch("restaurants/{restaurantId:guid}/suspend")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> SuspendRestaurantAsync(Guid restaurantId, CancellationToken ct)
    {
        var result = await sender.Send(new SuspendRestaurantCommand(restaurantId), ct);
        return result.IsSuccess ? Ok(ApiResponse.Ok(result.Value)) : result.Error.ToActionResult();
    }

    /// <summary>
    /// PATCH /api/v1/admin/restaurants/{restaurantId}/reactivate
    /// Restores a suspended restaurant to active. Only accepts restaurants currently in the
    /// Suspended state — a pending-approval account cannot be activated through this path.
    /// </summary>
    [HttpPatch("restaurants/{restaurantId:guid}/reactivate")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> ReactivateRestaurantAsync(Guid restaurantId, CancellationToken ct)
    {
        var result = await sender.Send(new ReactivateRestaurantCommand(restaurantId), ct);
        return result.IsSuccess ? Ok(ApiResponse.Ok(result.Value)) : result.Error.ToActionResult();
    }

    /// <summary>
    /// POST /api/v1/admin/restaurants/{restaurantId}/credit/settle
    /// Records a debt payment against a restaurant's outstanding credit balance.
    /// <c>paymentMethod</c> is required (<c>bank_transfer</c> | <c>manual</c>); <c>reference</c>
    /// is a required external reconciliation reference and must be unique per restaurant.
    /// </summary>
    [HttpPost("restaurants/{restaurantId:guid}/credit/settle")]
    [Authorize(Roles = "admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> SettleRestaurantCreditAsync(
        Guid restaurantId,
        [FromBody] SettleCreditRequest body,
        CancellationToken ct)
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (!Guid.TryParse(raw, out var adminId))
            return Unauthorized();

        if (!TryParsePaymentMethod(body.PaymentMethod, out var paymentMethod))
            return BadRequest(ApiResponse.Err("VALIDATION_ERROR",
                "'paymentMethod' must be one of: bank_transfer, manual."));

        var result = await sender.Send(
            new SettleRestaurantCreditCommand(
                restaurantId, adminId, body.Amount, paymentMethod, body.Reference, body.Note),
            ct);

        return result.IsSuccess ? Ok(ApiResponse.Ok(result.Value)) : result.Error.ToActionResult();
    }

    private static bool TryParsePaymentMethod(string? raw, out PaymentMethod paymentMethod)
    {
        switch (raw)
        {
            case "bank_transfer":
                paymentMethod = PaymentMethod.BankTransfer;
                return true;
            case "manual":
                paymentMethod = PaymentMethod.Manual;
                return true;
            default:
                paymentMethod = default;
                return false;
        }
    }

    [HttpPut("restaurants/{restaurantId:guid}/credit/limit")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> SetRestaurantCreditLimitAsync(
        Guid restaurantId,
        [FromBody] SetCreditLimitRequest body,
        CancellationToken ct)
    {
        var result = await sender.Send(
            new SetRestaurantCreditLimitCommand(restaurantId, body.CreditLimit, body.Note), ct);

        return result.IsSuccess ? Ok(ApiResponse.Ok(result.Value)) : result.Error.ToActionResult();
    }

    // ── Operational Settings (Admin) ──────────────────────────────────────────

    /// <summary>GET /api/v1/admin/operational-settings</summary>
    [HttpGet("operational-settings")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> GetOperationalSettingsAsync(CancellationToken ct)
    {
        var result = await sender.Send(new GetOperationalSettingsQuery(), ct);
        return result.IsSuccess ? Ok(ApiResponse.Ok(result.Value)) : result.Error.ToActionResult();
    }

    /// <summary>PUT /api/v1/admin/operational-settings</summary>
    [HttpPut("operational-settings")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> UpdateOperationalSettingsAsync(
        [FromBody] UpdateOperationalSettingsRequest body, CancellationToken ct)
    {
        var result = await sender.Send(
            new UpdateOperationalSettingsCommand(
                body.DailyCutoffTime, body.BatchingEnabled, body.DefaultRouteType,
                body.DeliveryWindowDays, body.DeliveryFeePerKm,
                body.BaseFee, body.MinimumFee, body.RoundingUnit),
            ct);
        return result.IsSuccess ? Ok(ApiResponse.Ok(result.Value)) : result.Error.ToActionResult();
    }
    // ── Procurement Batches (Admin) ─────────────────────────────────────────

    [HttpGet("market-sessions")]
    [Authorize(Roles = "admin,operations_manager")]
    public async Task<IActionResult> GetMarketSessionsAsync(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] Guid? marketId,
        [FromQuery] string? status,
        CancellationToken ct)
    {
        var result = await sender.Send(new GetMarketSessionsQuery(from, to, marketId, status), ct);
        return result.IsSuccess ? Ok(ApiResponse.Ok(result.Value)) : result.Error.ToActionResult();
    }

    [HttpGet("market-sessions/{id:guid}")]
    [Authorize(Roles = "admin,operations_manager")]
    public async Task<IActionResult> GetMarketSessionAsync(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new GetMarketSessionQuery(id), ct);
        return result.IsSuccess ? Ok(ApiResponse.Ok(result.Value)) : result.Error.ToActionResult();
    }

    [HttpPut("market-sessions/{id:guid}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> UpdateMarketSessionAsync(
        Guid id, [FromBody] UpdateMarketSessionRequest body, CancellationToken ct)
    {
        if (!TryGetActorId(out var actorId))
            return Unauthorized();
        var result = await sender.Send(new UpdateMarketSessionCommand(id, body.ClosesAt, actorId), ct);
        return result.IsSuccess ? Ok(ApiResponse.Ok(result.Value)) : result.Error.ToActionResult();
    }

    [HttpPost("market-sessions/{id:guid}/open")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> OpenMarketSessionAsync(Guid id, CancellationToken ct)
    {
        if (!TryGetActorId(out var actorId))
            return Unauthorized();
        var result = await sender.Send(new OpenMarketSessionCommand(id, actorId), ct);
        return result.IsSuccess ? Ok(ApiResponse.Ok(result.Value)) : result.Error.ToActionResult();
    }

    [HttpPost("market-sessions/{id:guid}/close")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> CloseMarketSessionAsync(
        Guid id, [FromBody] CloseMarketSessionRequest body, CancellationToken ct)
    {
        if (!TryGetActorId(out var actorId))
            return Unauthorized();
        var result = await sender.Send(new CloseMarketSessionCommand(id, actorId, body.Reason), ct);
        return result.IsSuccess ? Ok(ApiResponse.Ok(result.Value)) : result.Error.ToActionResult();
    }

    private bool TryGetActorId(out Guid actorId)
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(raw, out actorId);
    }

    [HttpGet("order-groups")]
    [Authorize(Roles = "admin,operations_manager")]
    public async Task<IActionResult> GetOrderGroupsAsync(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] DateOnly? date = null,
        [FromQuery] Guid? marketId = null,
        CancellationToken ct = default)
    {
        var result = await sender.Send(
            new GetProcurementBatchesQuery(page, pageSize, date, marketId),
            ct);
        return result.IsSuccess ? Ok(ApiResponse.Ok(result.Value)) : result.Error.ToActionResult();
    }

    [HttpGet("order-groups/progress")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> GetOrderGroupsProgressAsync(
        [FromQuery] DateOnly? date,
        [FromQuery] string? status,
        CancellationToken ct = default)
    {
        var result = await sender.Send(
            new GetProcurementProgressQuery(date, status),
            ct);
        return result.IsSuccess ? Ok(ApiResponse.Ok(result.Value)) : result.Error.ToActionResult();
    }

    [HttpPost("order-groups/auto-batch")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> RunAutoBatchAsync(
        [FromBody] RunAutoBatchRequest body,
        CancellationToken ct)
    {
        var result = await sender.Send(
            new RunAutoBatchCommand(
                body.TargetDate,
                body.DryRun ?? false,
                body.Force ?? false),
            ct);
        return result.IsSuccess ? Ok(ApiResponse.Ok(result.Value)) : result.Error.ToActionResult();
    }

    [HttpPost("order-groups/reset")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> ResetOrderGroupsAsync(
        [FromBody] ResetOrderGroupsRequest body,
        CancellationToken ct)
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (!Guid.TryParse(raw, out var adminId))
            return Unauthorized();

        var result = await sender.Send(
            new ResetBatchingDayCommand(body.TargetDate, body.Confirmation, adminId),
            ct);
        return result.IsSuccess ? Ok(ApiResponse.Ok(result.Value)) : result.Error.ToActionResult();
    }

    [HttpPost("order-groups/{batchId:guid}/manifest")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> GenerateManifestAsync(Guid batchId, CancellationToken ct)
    {
        var result = await sender.Send(new GenerateManifestCommand(batchId), ct);
        return result.IsSuccess ? Ok(ApiResponse.Ok(result.Value)) : result.Error.ToActionResult();
    }

    [HttpPut("batches/{batchId:guid}/item-assignments")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> AssignBatchItemsAsync(
        Guid batchId,
        [FromBody] AssignBatchItemsRequest body,
        CancellationToken ct)
    {
        var result = await sender.Send(
            new AssignBatchItemsCommand(batchId, body.Assignments),
            ct);
        return result.IsSuccess ? Ok(ApiResponse.Ok(result.Value)) : result.Error.ToActionResult();
    }

    /// <summary>
    /// POST /api/v1/admin/order-groups/{batchId}/cancel — cancels the session and every order it
    /// covers. Only allowed before the agent has bought anything.
    /// </summary>
    [HttpPost("order-groups/{batchId:guid}/cancel")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> CancelOrderGroupAsync(
        Guid batchId,
        [FromBody] CancelOrderGroupRequest body,
        CancellationToken ct)
    {
        var result = await sender.Send(new CancelBatchCommand(batchId, body.Reason), ct);
        return result.IsSuccess ? Ok(ApiResponse.Ok(result.Value)) : result.Error.ToActionResult();
    }

    // ── Audit Log (Admin) ──────────────────────────────────────────────────────

    /// <summary>GET /api/v1/admin/audit-logs — filter by actor/action/entity/time.</summary>
    [HttpGet("audit-logs")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> GetAuditLogsAsync(
        [FromQuery] Guid? actorId,
        [FromQuery] string? action,
        [FromQuery] string? entityType,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await sender.Send(
            new GetAuditLogsQuery(actorId, action, entityType, from, to, page, pageSize), ct);
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
public sealed record AssignBatchItemsRequest(IReadOnlyList<ItemAssignmentDto> Assignments);
public sealed record ReplaceMarketAssignmentsRequest(IReadOnlyList<Guid> MarketIds);
public sealed record SettleCreditRequest(decimal Amount, string? PaymentMethod, string? Reference, string? Note);
public sealed record SetCreditLimitRequest(decimal CreditLimit, string? Note);
public sealed record UpdateOperationalSettingsRequest(
    TimeOnly DailyCutoffTime,
    bool BatchingEnabled,
    string DefaultRouteType,
    int DeliveryWindowDays,
    decimal? DeliveryFeePerKm = null,
    decimal? BaseFee = null,
    decimal? MinimumFee = null,
    decimal? RoundingUnit = null);
public sealed record RunAutoBatchRequest(DateOnly? TargetDate, bool? DryRun, bool? Force);
public sealed record ResetOrderGroupsRequest(DateOnly TargetDate, string Confirmation);
public sealed record CancelOrderGroupRequest(string? Reason);
public sealed record UpdateMarketSessionRequest(DateTimeOffset ClosesAt);
public sealed record CloseMarketSessionRequest(string? Reason);
