using FreshFlow.API.Extensions;
using FreshFlow.Logistics.Application.Queries.EstimateOrderShipment;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FreshFlow.API.Controllers;

[ApiController]
[Route("api/v1/logistics/shipping")]
[Authorize(Roles = "admin,operations_manager")]
public sealed class ShippingController(ISender sender) : ControllerBase
{
    [HttpGet("orders/{orderId:guid}/estimate")]
    public async Task<IActionResult> EstimateOrderAsync(
        Guid orderId,
        [FromQuery(Name = "vehicle_id")] Guid? vehicleId = null,
        CancellationToken ct = default)
    {
        var result = await sender.Send(
            new EstimateOrderShipmentQuery(orderId, vehicleId), ct);
        return result.IsSuccess ? Ok(ApiResponse.Ok(result.Value)) : result.Error.ToActionResult();
    }
}
