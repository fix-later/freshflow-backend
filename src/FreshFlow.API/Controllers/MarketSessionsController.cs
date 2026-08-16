using FreshFlow.API.Extensions;
using FreshFlow.Procurement.Application.Queries.GetMarketSessions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FreshFlow.API.Controllers;

[ApiController]
[Route("api/v1/market-sessions")]
[Authorize]
public sealed class MarketSessionsController(ISender sender) : ControllerBase
{
    [HttpGet("availability")]
    [Authorize(Roles = "restaurant")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetAvailabilityAsync(
        [FromQuery] Guid marketId,
        [FromQuery] DateOnly serviceDate,
        CancellationToken ct)
    {
        if (marketId == Guid.Empty || serviceDate == default)
            return BadRequest(ApiResponse.Err(
                "VALIDATION_ERROR", "MarketId and serviceDate are required."));

        var result = await sender.Send(
            new GetMarketSessionsQuery(serviceDate, serviceDate, marketId, null), ct);
        if (result.IsFailure)
            return result.Error.ToActionResult();

        var session = result.Value.SingleOrDefault();
        return Ok(ApiResponse.Ok(new MarketSessionAvailabilityResponse(
            marketId,
            serviceDate,
            session is not null,
            session?.Status == "open",
            session?.Status,
            session?.Id)));
    }

    [HttpGet]
    public async Task<IActionResult> GetAsync(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] Guid? marketId,
        CancellationToken ct)
    {
        var result = await sender.Send(new GetMarketSessionsQuery(from, to, marketId, null), ct);
        if (result.IsFailure)
            return result.Error.ToActionResult();

        return Ok(ApiResponse.Ok(result.Value.Select(session => new
        {
            session.Id,
            session.MarketId,
            session.MarketName,
            session.ServiceDate,
            session.Status,
            session.ClosesAt
        })));
    }
}

public sealed record MarketSessionAvailabilityResponse(
    Guid MarketId,
    DateOnly ServiceDate,
    bool Exists,
    bool IsOpen,
    string? Status,
    Guid? SessionId);
