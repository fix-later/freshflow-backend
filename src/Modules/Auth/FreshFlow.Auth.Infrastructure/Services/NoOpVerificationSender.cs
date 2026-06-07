using FreshFlow.Auth.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace FreshFlow.Auth.Infrastructure.Services;

internal sealed class NoOpVerificationSender(ILogger<NoOpVerificationSender> logger) : IVerificationSender
{
    public Task SendVerificationCodeAsync(string email, string code, CancellationToken ct)
    {
        logger.LogInformation("[STUB] Verification code dispatched to {Email} — real email delivery deferred to v2.", email);
        return Task.CompletedTask;
    }
}
