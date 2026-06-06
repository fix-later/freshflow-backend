using FreshFlow.Auth.Application.Abstractions;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Auth.Application.Commands.RefreshToken;

internal sealed class RefreshTokenCommandHandler(
    IRefreshTokenRepository tokens,
    IUserRepository users,
    ITokenService tokenService) : IRequestHandler<RefreshTokenCommand, Result<RefreshTokenResponse>>
{
    public async Task<Result<RefreshTokenResponse>> Handle(RefreshTokenCommand request, CancellationToken ct)
    {
        var hash = tokenService.HashRefreshToken(request.RefreshToken);
        var stored = await tokens.FindByHashAsync(hash, ct);

        if (stored is null)
            return Result<RefreshTokenResponse>.Failure(
                Error.Unauthorized("REFRESH_TOKEN_REVOKED", "Refresh token not found or revoked."));

        if (stored.IsRevoked)
        {
            // Token reuse detected — revoke the entire family
            await tokens.RevokeByFamilyAsync(stored.FamilyId, ct);
            await tokens.SaveChangesAsync(ct);
            return Result<RefreshTokenResponse>.Failure(
                Error.Conflict("REFRESH_TOKEN_REUSE", "Refresh token has already been used."));
        }

        if (stored.IsExpired)
            return Result<RefreshTokenResponse>.Failure(
                Error.Unauthorized("REFRESH_TOKEN_EXPIRED", "Refresh token has expired."));

        var user = await users.FindByIdAsync(stored.UserId, ct);
        if (user is null || !user.CanLogin())
            return Result<RefreshTokenResponse>.Failure(
                Error.Unauthorized("UNAUTHORIZED", "User account is no longer active."));

        // Rotate: revoke old, issue new in same family
        var newRaw = tokenService.GenerateRefreshToken();
        var newHash = tokenService.HashRefreshToken(newRaw);
        var newToken = new Domain.Entities.RefreshToken(
            user.Id, newHash, stored.FamilyId, tokenService.RefreshTokenTtlDays);

        stored.Revoke(newToken.Id);
        await tokens.AddAsync(newToken, ct);
        await tokens.SaveChangesAsync(ct);

        var accessToken = tokenService.GenerateAccessToken(
            user.Id, user.Email, user.Role.Name);

        return Result<RefreshTokenResponse>.Success(
            new RefreshTokenResponse(accessToken, newRaw, tokenService.AccessTokenTtlSeconds));
    }
}
