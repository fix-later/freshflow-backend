using System.Security.Claims;
using FreshFlow.API.Extensions;
using FreshFlow.Orders.Application.Queries.GetCreditTransactions;
using FreshFlow.Orders.Application.Queries.GetRestaurantCredit;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FreshFlow.API.Controllers;

[ApiController]
[Route("api/v1/restaurants/{restaurantId:guid}/credit")]
[Authorize(Roles = "admin,restaurant")]
public sealed class RestaurantCreditController(ISender sender) : ControllerBase
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

    [HttpGet("transactions")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTransactionsAsync(Guid restaurantId, CancellationToken ct)
    {
        var result = await sender.Send(
            new GetCreditTransactionsQuery(ResolveUserId(), User.IsInRole("admin"), restaurantId), ct);

        return result.IsSuccess ? Ok(ApiResponse.Ok(result.Value)) : result.Error.ToActionResult();
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
