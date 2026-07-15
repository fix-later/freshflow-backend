using FreshFlow.Analytics.Application.Queries.GetDashboardOverview;
using FreshFlow.API.Extensions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FreshFlow.API.Controllers;

[ApiController]
[Route("api/v1/analytics")]
[Authorize]
public sealed class AnalyticsController(ISender sender) : ControllerBase
{
    [HttpGet("overview")]
    [Authorize(Roles = "admin,operations_manager")]
    public async Task<IActionResult> GetOverviewAsync(
        [FromQuery] DateOnly? date,
        CancellationToken ct)
    {
        var result = await sender.Send(new GetDashboardOverviewQuery(date), ct);
        return result.IsSuccess ? Ok(ApiResponse.Ok(result.Value)) : result.Error.ToActionResult();
    }
}

