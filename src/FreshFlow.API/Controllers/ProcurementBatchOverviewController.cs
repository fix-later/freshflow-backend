using FreshFlow.API.Extensions;
using FreshFlow.Procurement.Application.Queries.GetBatchOverview;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FreshFlow.API.Controllers;

[ApiController]
[Route("api/v1/procurement/batches")]
[Authorize(Roles = "admin,operations_manager")]
public sealed class ProcurementBatchOverviewController(ISender sender) : ControllerBase
{
    [HttpGet("{batchId:guid}/overview")]
    public async Task<IActionResult> GetAsync(Guid batchId, CancellationToken ct)
    {
        var result = await sender.Send(new GetBatchOverviewQuery(batchId), ct);
        return result.IsSuccess ? Ok(ApiResponse.Ok(result.Value)) : result.Error.ToActionResult();
    }
}
