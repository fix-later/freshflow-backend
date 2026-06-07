using FreshFlow.Auth.Application.Abstractions;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Auth.Application.Commands.ResetPassword;

internal sealed class ResetPasswordCommandHandler(
    IUserRepository users,
    IRefreshTokenRepository tokens,
    IPasswordResetTokenRepository resetTokens,
    IPasswordHasher hasher,
    ITokenService tokenService) : IRequestHandler<ResetPasswordCommand, Result>
{
    private static readonly Error TokenInvalid =
        Error.Validation("RESET_TOKEN_INVALID", "The reset token does not match any pending request.");

    private static readonly Error TokenExpired =
        Error.Validation("RESET_TOKEN_EXPIRED", "The reset token has expired or has already been used.");

    public async Task<Result> Handle(ResetPasswordCommand request, CancellationToken ct)
    {
        var tokenHash = tokenService.HashRefreshToken(request.Token);
        var resetToken = await resetTokens.FindByHashAsync(tokenHash, ct);

        if (resetToken is null)
            return Result.Failure(TokenInvalid);

        if (!resetToken.IsValid)
            return Result.Failure(TokenExpired);

        var user = await users.FindByIdAsync(resetToken.UserId, ct);
        if (user is null)
            return Result.Failure(TokenInvalid);

        var newHash = hasher.Hash(request.NewPassword);
        user.ChangePassword(newHash);
        resetToken.MarkUsed();

        await tokens.RevokeByUserAsync(user.Id, ct);
        await users.SaveChangesAsync(ct);

        return Result.Success();
    }
}
