using FreshFlow.Auth.Application.Abstractions;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Auth.Application.Commands.VerifyEmail;

internal sealed class VerifyEmailCommandHandler(
    IUserRepository users,
    IVerificationCodeRepository codes,
    ITokenService tokenService) : IRequestHandler<VerifyEmailCommand, Result>
{
    private static readonly Error ChannelNotSupported =
        Error.Validation("CHANNEL_NOT_SUPPORTED", "Only EMAIL channel is supported in v1.");

    private static readonly Error OtpInvalid =
        Error.Validation("OTP_INVALID", "The verification code is incorrect, expired, or already used.");

    public async Task<Result> Handle(VerifyEmailCommand request, CancellationToken ct)
    {
        if (!string.Equals(request.Channel, "EMAIL", StringComparison.OrdinalIgnoreCase))
            return Result.Failure(ChannelNotSupported);

        var email = request.Identifier.ToLowerInvariant();
        var codeHash = tokenService.HashRefreshToken(request.Code);

        var user = await users.FindByEmailAsync(email, ct);

        if (user is null || !user.IsActive)
            return Result.Failure(OtpInvalid);

        // Validate the code FIRST — prevents account-state oracle (M7).
        // An attacker without a valid code must never learn whether the email is already verified.
        var verification = await codes.FindByUserChannelAndHashAsync(user.Id, "EMAIL", codeHash, ct);

        if (verification is null || !verification.IsValid)
            return Result.Failure(OtpInvalid);

        // Idempotent: already verified is a success — but only reached with a valid code above.
        // Consume the code even on the idempotent path so it cannot be replayed until expiry.
        if (user.EmailVerifiedAt.HasValue)
        {
            verification.MarkUsed();
            await users.SaveChangesAsync(ct);
            return Result.Success();
        }

        verification.MarkUsed();
        user.MarkEmailVerified();

        await users.SaveChangesAsync(ct);

        return Result.Success();
    }
}
