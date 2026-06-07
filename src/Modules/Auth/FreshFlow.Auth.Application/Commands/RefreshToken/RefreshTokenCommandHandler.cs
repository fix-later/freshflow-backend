using FreshFlow.Auth.Application.Abstractions;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Auth.Application.Commands.RefreshToken;

internal sealed class RefreshTokenCommandHandler(
    IRefreshTokenRepository tokens,
    IUserRepository users,
    ITokenService tokenService) : IRequestHandler<RefreshTokenCommand, Result<RefreshTokenResponse>>
{
    private static readonly Error TokenInvalid =
        Error.Unauthorized("TOKEN_INVALID", "Refresh token is invalid or expired.");

    private static readonly Error TokenReuseDetected =
        Error.Unauthorized("TOKEN_REUSE_DETECTED", "Token reuse detected. All sessions invalidated.");

    public async Task<Result<RefreshTokenResponse>> Handle(RefreshTokenCommand request, CancellationToken ct)
    {
        var hash = tokenService.HashRefreshToken(request.RefreshToken);
        var stored = await tokens.FindByHashAsync(hash, ct);

        if (stored is null)
            return Result<RefreshTokenResponse>.Failure(TokenInvalid);

        if (stored.IsRevoked)
        {
            // Token reuse detected — invalidate the entire family to protect all sessions.
            await tokens.RevokeByFamilyAsync(stored.FamilyId, "family_compromised", ct);
            await tokens.SaveChangesAsync(ct);
            return Result<RefreshTokenResponse>.Failure(TokenReuseDetected);
        }

        if (stored.IsExpired)
            return Result<RefreshTokenResponse>.Failure(TokenInvalid);

        var user = await users.FindByIdAsync(stored.UserId, ct);
        if (user is null || !user.CanLogin())
            return Result<RefreshTokenResponse>.Failure(
                Error.Unauthorized("UNAUTHORIZED", "User account is no longer active."));

        // Rotate: revoke old token (with reason), issue new in same family.
        var newRaw = tokenService.GenerateRefreshToken();
        var newHash = tokenService.HashRefreshToken(newRaw);
        var newToken = new Domain.Entities.RefreshToken(
            user.Id, newHash, stored.FamilyId, tokenService.RefreshTokenTtlDays);

        stored.Revoke(newToken.Id, reason: "rotated");
        await tokens.AddAsync(newToken, ct);
        await tokens.SaveChangesAsync(ct);

        var accessToken = tokenService.GenerateAccessToken(
            user.Id, user.Email, user.Role.Name);

        return Result<RefreshTokenResponse>.Success(
            new RefreshTokenResponse(accessToken, newRaw, tokenService.AccessTokenTtlSeconds));
    }
}
