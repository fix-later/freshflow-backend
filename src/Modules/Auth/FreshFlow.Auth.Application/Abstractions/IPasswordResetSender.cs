namespace FreshFlow.Auth.Application.Abstractions;

/// <summary>
/// Dispatches the password-reset link to the user's registered email address.
/// The v1 implementation is a no-op stub; replace with a real email provider in a later sprint.
/// </summary>
public interface IPasswordResetSender
{
    /// <param name="email">The recipient's email address.</param>
    /// <param name="rawToken">The plain-text reset token to embed in the link.</param>
    public Task SendResetLinkAsync(string email, string rawToken, CancellationToken ct);
}
