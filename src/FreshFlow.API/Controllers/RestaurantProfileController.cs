using System.Security.Claims;
using FreshFlow.API.Extensions;
using FreshFlow.Auth.Application.Commands.UpdateRestaurantProfile;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FreshFlow.API.Controllers;

[ApiController]
[Route("api/v1/restaurants")]
[Authorize(Roles = "restaurant")]
public sealed class RestaurantProfileController(ISender sender) : ControllerBase
{
    /// <summary>PUT /api/v1/restaurants/me/profile — updates the authenticated restaurant's profile.</summary>
    [HttpPut("me/profile")]
    public async Task<IActionResult> UpdateProfile(
        [FromBody] UpdateRestaurantProfileRequest body, CancellationToken ct)
    {
        var userId = Guid.Parse(
            User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub")
            ?? throw new UnauthorizedAccessException("User ID claim missing."));

        var result = await sender.Send(
            new UpdateRestaurantProfileCommand(
                userId,
                body.Name,
                body.Address,
                body.ContactPerson,
                body.PickupStart,
                body.PickupEnd),
            ct);

        return result.IsSuccess ? Ok(result.Value) : result.Error.ToActionResult();
    }
}

public sealed record UpdateRestaurantProfileRequest(
    string Name,
    string? Address,
    string? ContactPerson,
    TimeOnly? PickupStart,
    TimeOnly? PickupEnd);
