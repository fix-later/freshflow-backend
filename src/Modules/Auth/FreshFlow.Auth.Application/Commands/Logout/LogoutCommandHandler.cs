using FreshFlow.Auth.Application.Abstractions;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Auth.Application.Commands.Logout;

internal sealed class LogoutCommandHandler(
    IRefreshTokenRepository tokens,
    ITokenService tokenService) : IRequestHandler<LogoutCommand, Result>
{
    private static readonly Error TokenNotFound =
        new("TOKEN_NOT_FOUND", "Refresh token not found or already revoked.");

    public async Task<Result> Handle(LogoutCommand request, CancellationToken ct)
    {
        var hash = tokenService.HashRefreshToken(request.RefreshToken);
        var stored = await tokens.FindByHashAsync(hash, ct);

        if (stored is null)
            return Result.Failure(TokenNotFound);

        // Ownership guard — same error code to avoid oracle leak on token existence.
        if (stored.UserId != request.UserId)
            return Result.Failure(TokenNotFound);

        // Already revoked → idempotent success (session is already terminated; no security risk).
        if (stored.IsRevoked)
            return Result.Success();

        stored.Revoke(reason: "logout");
        await tokens.SaveChangesAsync(ct);

        return Result.Success();
    }
}
