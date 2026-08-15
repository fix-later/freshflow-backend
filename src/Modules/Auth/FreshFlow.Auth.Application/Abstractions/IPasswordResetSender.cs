namespace FreshFlow.Auth.Application.Abstractions;

/// <summary>
/// Dispatches a password-reset OTP to the user's registered email address.
/// </summary>
public interface IPasswordResetSender
{
    /// <param name="email">The recipient's email address.</param>
    /// <param name="code">The plain-text six-digit OTP.</param>
    public Task SendResetCodeAsync(string email, string code, CancellationToken ct);
}
