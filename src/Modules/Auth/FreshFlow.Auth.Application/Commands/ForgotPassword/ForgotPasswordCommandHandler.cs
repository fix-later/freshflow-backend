using System.Security.Cryptography;
using FreshFlow.Auth.Application.Abstractions;
using FreshFlow.Auth.Domain.Entities;
using FreshFlow.SharedKernel.Application;
using MediatR;
using Microsoft.Extensions.Logging;

namespace FreshFlow.Auth.Application.Commands.ForgotPassword;

internal sealed class ForgotPasswordCommandHandler(
    IUserRepository users,
    IPasswordResetTokenRepository resetTokens,
    IPasswordResetSender sender,
    IPasswordHasher passwordHasher,
    ILogger<ForgotPasswordCommandHandler> logger) : IRequestHandler<ForgotPasswordCommand, Result>
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

        var rawCode = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        var tokenHash = passwordHasher.Hash(rawCode);

        var resetToken = PasswordResetToken.Create(user.Id, tokenHash);
        await resetTokens.AddAsync(resetToken, ct);
        await resetTokens.SaveChangesAsync(ct);

        // Dispatch reset code — swallow delivery failures to prevent account-existence oracle:
        // a Resend API outage must not produce a 500 that distinguishes known from unknown emails.
        // The exception-filter re-raises OperationCanceledException so cancellation propagates normally.
        try
        {
            await sender.SendResetCodeAsync(user.Email, rawCode, ct);
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            // Log with userId context — NOT the email — so logs are useful without leaking PII (M8).
            logger.LogWarning(
                "Password reset email delivery failed for user {UserId}. Delivery errors are swallowed to prevent account-existence oracle.",
                user.Id);
        }

        return Result.Success();
    }
}
