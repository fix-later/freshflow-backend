using System.Security.Claims;
using FreshFlow.API.Extensions;
using FreshFlow.Auth.Application.Commands.ChangePassword;
using FreshFlow.Auth.Application.Commands.ForgotPassword;
using FreshFlow.Auth.Application.Commands.Login;
using FreshFlow.Auth.Application.Commands.Logout;
using FreshFlow.Auth.Application.Commands.RefreshToken;
using FreshFlow.Auth.Application.Commands.RequestVerification;
using FreshFlow.Auth.Application.Commands.VerifyEmail;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FreshFlow.API.Controllers;

[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController(ISender sender) : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest body, CancellationToken ct)
    {
        var result = await sender.Send(new LoginCommand(body.Identifier, body.Password), ct);
        return result.IsSuccess
            ? Ok(result.Value)
            : result.Error.ToActionResult();
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest body, CancellationToken ct)
    {
        var result = await sender.Send(new RefreshTokenCommand(body.RefreshToken), ct);
        return result.IsSuccess
            ? Ok(result.Value)
            : result.Error.ToActionResult();
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout([FromBody] LogoutRequest body, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? User.FindFirstValue("sub")
                     ?? throw new UnauthorizedAccessException();

        var result = await sender.Send(
            new LogoutCommand(Guid.Parse(userId), body.RefreshToken), ct);

        return result.IsSuccess ? NoContent() : result.Error.ToActionResult();
    }

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest body, CancellationToken ct)
    {
        var result = await sender.Send(new ForgotPasswordCommand(body.Identifier), ct);

        // Always 202 for valid requests — even when email does not match any account.
        return result.IsSuccess ? Accepted() : result.Error.ToActionResult();
    }

    [HttpPost("verify/request")]
    [AllowAnonymous]
    public async Task<IActionResult> RequestVerification([FromBody] RequestVerificationRequest body, CancellationToken ct)
    {
        var result = await sender.Send(new RequestVerificationCommand(body.Identifier, body.Channel), ct);
        return result.IsSuccess ? Accepted() : result.Error.ToActionResult();
    }

    [HttpPost("verify")]
    [AllowAnonymous]
    public async Task<IActionResult> Verify([FromBody] VerifyRequest body, CancellationToken ct)
    {
        var result = await sender.Send(new VerifyEmailCommand(body.Identifier, body.Channel, body.Code), ct);
        return result.IsSuccess ? Ok() : result.Error.ToActionResult();
    }

    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest body, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? User.FindFirstValue("sub")
                     ?? throw new UnauthorizedAccessException();

        var result = await sender.Send(
            new ChangePasswordCommand(Guid.Parse(userId), body.CurrentPassword, body.NewPassword), ct);

        return result.IsSuccess ? NoContent() : result.Error.ToActionResult();
    }
}

/// <param name="Identifier">Email address or phone number.</param>
public sealed record LoginRequest(string Identifier, string Password);
public sealed record ForgotPasswordRequest(string Identifier);
public sealed record RefreshRequest(string RefreshToken);
public sealed record LogoutRequest(string RefreshToken);
public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
public sealed record RequestVerificationRequest(string Identifier, string Channel);
public sealed record VerifyRequest(string Identifier, string Channel, string Code);
