using FreshFlow.Auth.Application.Abstractions;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Auth.Application.Commands.Logout;

internal sealed class LogoutCommandHandler(
    IRefreshTokenRepository tokens,
    ITokenService tokenService) : IRequestHandler<LogoutCommand, Result>
{
    public async Task<Result> Handle(LogoutCommand request, CancellationToken ct)
    {
        var hash = tokenService.HashRefreshToken(request.RefreshToken);
        var stored = await tokens.FindByHashAsync(hash, ct);

        // Silently succeed even if token not found or already revoked — prevents oracle attacks
        if (stored is null || stored.IsRevoked)
            return Result.Success();

        stored.Revoke();
        await tokens.SaveChangesAsync(ct);

        return Result.Success();
    }
}
