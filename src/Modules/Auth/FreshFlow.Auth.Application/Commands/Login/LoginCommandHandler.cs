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
    private static readonly Error InvalidCredentials =
        Error.Unauthorized("INVALID_CREDENTIALS", "Identifier or password is incorrect.");

    private static Error BuildAccountLockedError(DateTime? lockedUntil) =>
        Error.Conflict("ACCOUNT_LOCKED", $"Account locked until {lockedUntil:O}.");

    private static readonly Error AccountInactive =
        Error.Validation("ACCOUNT_INACTIVE", "This account has been deactivated.");

    public async Task<Result<LoginResponse>> Handle(LoginCommand request, CancellationToken ct)
    {
        var user = await users.FindByIdentifierAsync(request.Identifier, ct);

        if (user is null)
            return Result<LoginResponse>.Failure(InvalidCredentials);

        if (user.IsLockedOut)
            return Result<LoginResponse>.Failure(BuildAccountLockedError(user.LockedUntil));

        if (!hasher.Verify(request.Password, user.PasswordHash))
        {
            user.RecordFailedLogin();
            await users.SaveChangesAsync(ct);
            return Result<LoginResponse>.Failure(InvalidCredentials);
        }

        if (!user.IsActive)
            return Result<LoginResponse>.Failure(AccountInactive);

        if (user.Role.Name == RoleNames.Restaurant)
        {
            // Restaurant login block checked via restaurant approval status — handled downstream.
            // For now, IsActive check above covers the deactivation path.
        }

        // Successful login — reset failed-attempt counter (LockedUntil auto-expires).
        user.RecordSuccessfulLogin();

        var accessToken = tokenService.GenerateAccessToken(
            user.Id, user.Email, user.Role.Name);

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
            new LoginUserDto(user.Id, user.Email, user.Role.Name)));
    }
}
