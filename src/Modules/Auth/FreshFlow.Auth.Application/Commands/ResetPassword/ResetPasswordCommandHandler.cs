using FreshFlow.Auth.Application.Abstractions;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Auth.Application.Commands.ResetPassword;

internal sealed class ResetPasswordCommandHandler(
    IUserRepository users,
    IRefreshTokenRepository tokens,
    IPasswordResetTokenRepository resetTokens,
    IPasswordHasher hasher) : IRequestHandler<ResetPasswordCommand, Result>
{
    private static readonly Error OtpInvalid =
        Error.Validation("RESET_OTP_INVALID", "The reset code is incorrect, expired, or already used.");

    public async Task<Result> Handle(ResetPasswordCommand request, CancellationToken ct)
    {
        var user = await users.FindByEmailAsync(request.Identifier.ToLowerInvariant(), ct);

        if (user is null || !user.IsActive)
            return Result.Failure(OtpInvalid);

        var resetToken = await resetTokens.FindLatestPendingByUserIdAsync(user.Id, ct);

        if (resetToken is null || !resetToken.IsValid || !hasher.Verify(request.Code, resetToken.TokenHash))
            return Result.Failure(OtpInvalid);

        var newHash = hasher.Hash(request.NewPassword);
        user.ChangePassword(newHash);
        resetToken.MarkUsed();

        await tokens.RevokeByUserAsync(user.Id, "PASSWORD_RESET", ct);
        await users.SaveChangesAsync(ct);

        return Result.Success();
    }
}
