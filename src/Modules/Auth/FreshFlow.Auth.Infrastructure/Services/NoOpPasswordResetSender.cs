using FreshFlow.Auth.Application.Abstractions;

namespace FreshFlow.Auth.Infrastructure.Services;

/// <summary>
/// Stub implementation of <see cref="IPasswordResetSender"/> for v1.
/// Discards the reset code silently. Replace with a real email provider in a later sprint.
/// </summary>
internal sealed class NoOpPasswordResetSender : IPasswordResetSender
{
    public Task SendResetCodeAsync(string email, string code, CancellationToken ct) =>
        Task.CompletedTask;
}
