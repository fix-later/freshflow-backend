using FreshFlow.Auth.Application.Abstractions;
using FreshFlow.Auth.Domain.Entities;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Auth.Application.Commands.ForgotPassword;

internal sealed class ForgotPasswordCommandHandler(
    IUserRepository users,
    IPasswordResetTokenRepository resetTokens,
    IPasswordResetSender sender,
    ITokenService tokenService) : IRequestHandler<ForgotPasswordCommand, Result>
{
    public async Task<Result> Handle(ForgotPasswordCommand request, CancellationToken ct)
    {
        var email = request.Identifier.ToLowerInvariant();
        var user = await users.FindByEmailAsync(email, ct);

        // Anti-oracle: always return success regardless of whether the email matches an account.
        if (user is null || !user.IsActive)
            return Result.Success();

        // Invalidate any prior pending credential for this user.
        await resetTokens.InvalidatePendingAsync(user.Id, ct);

        // Generate a fresh single-use token (same algorithm as refresh tokens).
        var rawToken = tokenService.GenerateRefreshToken();
        var tokenHash = tokenService.HashRefreshToken(rawToken);

        var resetToken = PasswordResetToken.Create(user.Id, tokenHash);
        await resetTokens.AddAsync(resetToken, ct);
        await resetTokens.SaveChangesAsync(ct);

        // Dispatch reset link — no-op stub in v1.
        await sender.SendResetLinkAsync(user.Email, rawToken, ct);

        return Result.Success();
    }
}
