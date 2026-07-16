using FreshFlow.Analytics.Application.Queries.GetDashboardOverview;
using FreshFlow.Analytics.Application.Queries.GetOrderMetrics;
using FreshFlow.Analytics.Application.Queries.GetPriceTrends;
using FreshFlow.API.Extensions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

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

    [HttpGet("price-trends")]
    [Authorize(Roles = "admin,operations_manager,restaurant")]
    public async Task<IActionResult> GetPriceTrendsAsync(
        [FromQuery(Name = "marketProductId")] Guid[] marketProductIds,
        [FromQuery, BindRequired] DateOnly from,
        [FromQuery, BindRequired] DateOnly to,
        [FromQuery] string? interval,
        CancellationToken ct)
    {
        var result = await sender.Send(
            new GetPriceTrendsQuery(marketProductIds, from, to, interval),
            ct);
        return result.IsSuccess ? Ok(ApiResponse.Ok(result.Value)) : result.Error.ToActionResult();
    }

    [HttpGet("order-metrics")]
    [Authorize(Roles = "admin,operations_manager")]
    public async Task<IActionResult> GetOrderMetricsAsync(
        [FromQuery, BindRequired] DateOnly from,
        [FromQuery, BindRequired] DateOnly to,
        [FromQuery] Guid? restaurantId,
        [FromQuery] string? groupBy,
        CancellationToken ct)
    {
        var result = await sender.Send(
            new GetOrderMetricsQuery(from, to, restaurantId, groupBy),
            ct);
        return result.IsSuccess ? Ok(ApiResponse.Ok(result.Value)) : result.Error.ToActionResult();
    }
}

