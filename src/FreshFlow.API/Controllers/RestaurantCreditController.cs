using System.Globalization;
using System.Security.Claims;
using FreshFlow.API.Extensions;
using FreshFlow.Orders.Application.Abstractions;
using FreshFlow.Orders.Application.Commands.GenerateCreditStatement;
using FreshFlow.Orders.Application.Queries.GetCreditStatement;
using FreshFlow.Orders.Application.Queries.GetCreditTransactions;
using FreshFlow.Orders.Application.Queries.GetRestaurantCredit;
using FreshFlow.Orders.Application.Queries.ListCreditStatements;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FreshFlow.API.Controllers;

[ApiController]
[Route("api/v1/restaurants/{restaurantId:guid}/credit")]
[Authorize(Roles = "admin,restaurant")]
public sealed class RestaurantCreditController(ISender sender, IStatementPdfRenderer pdfRenderer) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCreditAsync(Guid restaurantId, CancellationToken ct)
    {
        var result = await sender.Send(
            new GetRestaurantCreditQuery(ResolveUserId(), User.IsInRole("admin"), restaurantId), ct);

        return result.IsSuccess ? Ok(ApiResponse.Ok(result.Value)) : result.Error.ToActionResult();
    }

    /// <summary>
    /// GET /api/v1/restaurants/{restaurantId}/credit/transactions
    /// Returns the cursor-paginated credit transaction (balance ledger) history for a restaurant.
    /// Admin or the owning restaurant only. Optional date filters: <c>from</c> / <c>to</c> (ISO 8601, inclusive).
    /// </summary>
    [HttpGet("transactions")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTransactionsAsync(
        Guid restaurantId,
        [FromQuery] string? cursor = null,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? from = null,
        [FromQuery] string? to = null,
        CancellationToken ct = default)
    {
        // Parse optional date range — return 400 VALIDATION_ERROR for bad format.
        // DateTimeOffset.TryParse + .UtcDateTime correctly handles timezone-offset inputs
        // (e.g. "2026-06-14T17:00:00+07:00" → UTC 2026-06-14T10:00:00Z).
        DateTime? parsedFrom = null;
        if (from is not null)
        {
            if (!DateTimeOffset.TryParse(from, null, DateTimeStyles.RoundtripKind, out var pf))
                return BadRequest(ApiResponse.Err("VALIDATION_ERROR",
                    "'from' is not a valid ISO 8601 date."));
            parsedFrom = pf.UtcDateTime;
        }

        DateTime? parsedTo = null;
        if (to is not null)
        {
            if (!DateTimeOffset.TryParse(to, null, DateTimeStyles.RoundtripKind, out var pt))
                return BadRequest(ApiResponse.Err("VALIDATION_ERROR",
                    "'to' is not a valid ISO 8601 date."));
            parsedTo = pt.UtcDateTime;
        }

        var result = await sender.Send(
            new GetCreditTransactionsQuery(
                ResolveUserId(), User.IsInRole("admin"), restaurantId, cursor, pageSize, parsedFrom, parsedTo),
            ct);

        return result.IsSuccess
            ? Ok(ApiResponse.OkPaged(result.Value.Items, result.Value.PageSize, result.Value.NextCursor))
            : result.Error.ToActionResult();
    }

    /// <summary>
    /// POST /api/v1/restaurants/{restaurantId}/credit/statements/generate
    /// Generates the immutable credit statement for the given (Asia/Ho_Chi_Minh) billing
    /// period. Idempotent — regenerating an already-generated period returns the existing
    /// statement. Admin or the owning restaurant only.
    /// </summary>
    [HttpPost("statements/generate")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GenerateStatementAsync(
        Guid restaurantId, [FromBody] GenerateStatementRequest body, CancellationToken ct)
    {
        var result = await sender.Send(
            new GenerateCreditStatementCommand(
                ResolveUserId(), User.IsInRole("admin"), restaurantId, body.Year, body.Month),
            ct);

        return result.IsSuccess ? Ok(ApiResponse.Ok(result.Value)) : result.Error.ToActionResult();
    }

    /// <summary>
    /// GET /api/v1/restaurants/{restaurantId}/credit/statements/{statementId}
    /// Returns a single credit statement (with line items). Admin or the owning restaurant only.
    /// </summary>
    [HttpGet("statements/{statementId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStatementAsync(Guid restaurantId, Guid statementId, CancellationToken ct)
    {
        var result = await sender.Send(
            new GetCreditStatementQuery(
                ResolveUserId(), User.IsInRole("admin"), restaurantId, StatementId: statementId),
            ct);

        return result.IsSuccess ? Ok(ApiResponse.Ok(result.Value)) : result.Error.ToActionResult();
    }

    /// <summary>
    /// GET /api/v1/restaurants/{restaurantId}/credit/statements/{statementId}/pdf
    /// Renders the same statement as <see cref="GetStatementAsync"/> to a downloadable PDF.
    /// Admin or the owning restaurant only — same IDOR guard as the JSON lookup.
    /// </summary>
    [HttpGet("statements/{statementId:guid}/pdf")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStatementPdfAsync(Guid restaurantId, Guid statementId, CancellationToken ct)
    {
        var result = await sender.Send(
            new GetCreditStatementQuery(
                ResolveUserId(), User.IsInRole("admin"), restaurantId, StatementId: statementId),
            ct);

        if (result.IsFailure)
            return result.Error.ToActionResult();

        var pdfBytes = pdfRenderer.Render(result.Value);
        return File(pdfBytes, "application/pdf", $"statement-{statementId}.pdf");
    }

    /// <summary>
    /// GET /api/v1/restaurants/{restaurantId}/credit/statements
    /// Returns the cursor-paginated statement history for a restaurant, most recent period
    /// first. Admin or the owning restaurant only.
    /// </summary>
    [HttpGet("statements")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ListStatementsAsync(
        Guid restaurantId,
        [FromQuery] string? cursor = null,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        var result = await sender.Send(
            new ListCreditStatementsQuery(ResolveUserId(), User.IsInRole("admin"), restaurantId, cursor, pageSize),
            ct);

        return result.IsSuccess
            ? Ok(ApiResponse.OkPaged(result.Value.Items, result.Value.PageSize, result.Value.NextCursor))
            : result.Error.ToActionResult();
    }

    private Guid ResolveUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? User.FindFirstValue("sub");

        return Guid.TryParse(raw, out var id)
            ? id
            : throw new UnauthorizedAccessException("User ID claim is missing or malformed.");
    }
}

/// <summary>Request body for POST .../credit/statements/generate.</summary>
public sealed record GenerateStatementRequest(int Year, int Month);
