using System.Security.Claims;
using FreshFlow.API.Extensions;
using FreshFlow.Notifications.Application.Commands.RegisterDevice;
using FreshFlow.Notifications.Application.Commands.UnregisterDevice;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FreshFlow.API.Controllers;

[ApiController]
[Route("api/v1/notifications/devices")]
[Authorize]
public sealed class NotificationDeviceController(ISender sender) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RegisterDeviceAsync(
        [FromBody] RegisterNotificationDeviceRequest body,
        CancellationToken ct)
    {
        var result = await sender.Send(
            new RegisterDeviceCommand(ResolveUserId(), body.Token, body.Platform, body.DeviceId),
            ct);

        return result.IsSuccess ? Ok(ApiResponse.Ok(result.Value)) : result.Error.ToActionResult();
    }

    [HttpDelete("{token}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UnregisterDeviceAsync(string token, CancellationToken ct)
    {
        var result = await sender.Send(new UnregisterDeviceCommand(ResolveUserId(), token), ct);
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

public sealed record RegisterNotificationDeviceRequest(
    string Token,
    string Platform,
    string? DeviceId);
