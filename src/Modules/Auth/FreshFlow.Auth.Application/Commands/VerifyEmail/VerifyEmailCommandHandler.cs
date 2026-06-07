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
        var user = await users.FindByEmailAsync(email, ct);

        if (user is null || !user.IsActive)
            return Result.Failure(OtpInvalid);

        // Idempotent: already verified is a success.
        if (user.EmailVerifiedAt.HasValue)
            return Result.Success();

        var codeHash = tokenService.HashRefreshToken(request.Code);
        var verification = await codes.FindByUserChannelAndHashAsync(user.Id, "EMAIL", codeHash, ct);

        if (verification is null || !verification.IsValid)
            return Result.Failure(OtpInvalid);

        verification.MarkUsed();
        user.MarkEmailVerified();

        await users.SaveChangesAsync(ct);

        return Result.Success();
    }
}
