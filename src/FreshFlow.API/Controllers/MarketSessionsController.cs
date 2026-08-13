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
