using System.Security.Claims;
using FreshFlow.API.Extensions;
using FreshFlow.Logistics.Application.Queries.GetDriverRoutesToday;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FreshFlow.API.Controllers;

[ApiController]
[Route("api/v1/driver")]
[Authorize(Roles = "driver")]
public sealed class DriverController(ISender sender) : ControllerBase
{
    [HttpGet("routes/today")]
    public async Task<IActionResult> GetRoutesTodayAsync(CancellationToken ct)
    {
        var result = await sender.Send(new GetDriverRoutesTodayQuery(ResolveUserId()), ct);
        return result.IsSuccess ? Ok(ApiResponse.Ok(result.Value)) : result.Error.ToActionResult();
    }

    private Guid ResolveUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? User.FindFirstValue("sub");
        return Guid.Parse(raw!);
    }
}
