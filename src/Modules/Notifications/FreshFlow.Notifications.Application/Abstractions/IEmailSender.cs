using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Notifications.Application.Abstractions;

/// <summary>
/// Sends a transactional email. Mirrors <see cref="IPushSender"/>: a config-gated SMTP
/// implementation in Infrastructure, with a log-only fallback when SMTP is not configured so
/// the app runs (and tests pass) without a mail server. Never throws for a delivery failure —
/// returns a failed <see cref="Result"/> so callers can log and carry on.
/// </summary>
public interface IEmailSender
{
    public Task<Result> SendAsync(
        string toEmail, string subject, string body, CancellationToken ct, EmailAttachment? attachment = null);
}
