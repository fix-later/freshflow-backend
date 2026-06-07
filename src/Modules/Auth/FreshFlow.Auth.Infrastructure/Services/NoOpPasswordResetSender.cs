using FreshFlow.Auth.Application.Abstractions;

namespace FreshFlow.Auth.Infrastructure.Services;

/// <summary>
/// Stub implementation of <see cref="IPasswordResetSender"/> for v1.
/// Discards the reset link silently. Replace with a real email provider in a later sprint.
/// </summary>
internal sealed class NoOpPasswordResetSender : IPasswordResetSender
{
    public Task SendResetLinkAsync(string email, string rawToken, CancellationToken ct) =>
        Task.CompletedTask;
}
