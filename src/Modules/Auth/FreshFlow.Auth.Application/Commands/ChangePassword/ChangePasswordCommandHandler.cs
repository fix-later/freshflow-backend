using FreshFlow.Auth.Application.Abstractions;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Auth.Application.Commands.ChangePassword;

internal sealed class ChangePasswordCommandHandler(
    IUserRepository users,
    IRefreshTokenRepository tokens,
    IPasswordHasher hasher) : IRequestHandler<ChangePasswordCommand, Result>
{
    public async Task<Result> Handle(ChangePasswordCommand request, CancellationToken ct)
    {
        var user = await users.FindByIdAsync(request.UserId, ct);
        if (user is null || !user.CanLogin())
            return Result.Failure(
                Error.Unauthorized("UNAUTHORIZED", "User account is no longer active."));

        if (!hasher.Verify(request.CurrentPassword, user.PasswordHash))
            return Result.Failure(
                Error.Unauthorized("INVALID_CURRENT_PASSWORD", "Current password is incorrect."));

        if (request.CurrentPassword == request.NewPassword)
            return Result.Failure(
                Error.Validation("VALIDATION_ERROR", "New password must be different from current password."));

        var newPasswordHash = hasher.Hash(request.NewPassword);
        user.ChangePassword(newPasswordHash);

        // Revoke all sessions before persisting the password change so that
        // the token revocation and the new password hash are committed together.
        // A single SaveChangesAsync covers both the user update and the token revocations,
        // preventing old sessions from remaining valid if persistence partially fails.
        await tokens.RevokeByUserAsync(user.Id, "PASSWORD_CHANGED", ct);
        await users.SaveChangesAsync(ct);

        return Result.Success();
    }
}
