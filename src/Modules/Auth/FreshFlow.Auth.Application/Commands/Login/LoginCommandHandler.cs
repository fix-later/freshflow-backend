using FreshFlow.Auth.Application.Abstractions;
using FreshFlow.Auth.Domain.Enums;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Auth.Application.Commands.Login;

internal sealed class LoginCommandHandler(
    IUserRepository users,
    IRefreshTokenRepository tokens,
    IPasswordHasher hasher,
    ITokenService tokenService) : IRequestHandler<LoginCommand, Result<LoginResponse>>
{
    public async Task<Result<LoginResponse>> Handle(LoginCommand request, CancellationToken ct)
    {
        var user = await users.FindByEmailAsync(request.Email, ct);

        if (user is null || !hasher.Verify(request.Password, user.PasswordHash))
            return Result<LoginResponse>.Failure(
                Error.Unauthorized("INVALID_CREDENTIALS", "Email or password is incorrect."));

        if (!user.IsActive)
            return Result<LoginResponse>.Failure(
                Error.Validation("ACCOUNT_INACTIVE", "This account has been deactivated."));

        if (user.Role == UserRole.Restaurant)
        {
            // Restaurant login block checked via restaurant approval status — handled downstream.
            // For now, IsActive check above covers the deactivation path.
        }

        var accessToken = tokenService.GenerateAccessToken(
            user.Id, user.Email, user.Role.ToApiString());

        var rawRefresh = tokenService.GenerateRefreshToken();
        var refreshHash = tokenService.HashRefreshToken(rawRefresh);
        var familyId = Guid.NewGuid();
        var refreshToken = new Domain.Entities.RefreshToken(
            user.Id, refreshHash, familyId, tokenService.RefreshTokenTtlDays);

        await tokens.AddAsync(refreshToken, ct);
        await tokens.SaveChangesAsync(ct);

        return Result<LoginResponse>.Success(new LoginResponse(
            accessToken,
            rawRefresh,
            tokenService.AccessTokenTtlSeconds,
            new LoginUserDto(user.Id, user.Email, user.Role.ToApiString())));
    }
}
