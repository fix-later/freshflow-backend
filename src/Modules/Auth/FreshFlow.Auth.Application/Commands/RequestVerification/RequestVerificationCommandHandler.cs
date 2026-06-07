using System.Security.Cryptography;
using FreshFlow.Auth.Application.Abstractions;
using FreshFlow.Auth.Domain.Entities;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Auth.Application.Commands.RequestVerification;

internal sealed class RequestVerificationCommandHandler(
    IUserRepository users,
    IVerificationCodeRepository codes,
    IVerificationSender sender,
    ITokenService tokenService) : IRequestHandler<RequestVerificationCommand, Result>
{
    private static readonly Error ChannelNotSupported =
        Error.Validation("CHANNEL_NOT_SUPPORTED", "Only EMAIL channel is supported in v1.");

    public async Task<Result> Handle(RequestVerificationCommand request, CancellationToken ct)
    {
        if (!string.Equals(request.Channel, "EMAIL", StringComparison.OrdinalIgnoreCase))
            return Result.Failure(ChannelNotSupported);

        var email = request.Identifier.ToLowerInvariant();
        var user = await users.FindByEmailAsync(email, ct);

        // Anti-oracle: return success regardless of whether the account exists.
        if (user is null || !user.IsActive)
            return Result.Success();

        await codes.InvalidatePendingAsync(user.Id, "EMAIL", ct);

        var rawCode = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        var codeHash = tokenService.HashRefreshToken(rawCode);

        var verificationCode = VerificationCode.Create(user.Id, "EMAIL", codeHash);
        await codes.AddAsync(verificationCode, ct);
        await codes.SaveChangesAsync(ct);

        try
        {
            await sender.SendVerificationCodeAsync(user.Email, rawCode, ct);
        }
        catch
        {
            // Swallow delivery failures — they must not leak account existence to the caller.
        }

        return Result.Success();
    }
}
