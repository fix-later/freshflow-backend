using FreshFlow.Notifications.Application.Abstractions;
using FreshFlow.SharedKernel.Application;
using Microsoft.Extensions.Logging;

namespace FreshFlow.Notifications.Infrastructure.Email;

/// <summary>
/// Fallback <see cref="IEmailSender"/> used when SMTP is not configured — logs instead of
/// sending so the app runs in dev/test without a mail server. Mirrors
/// <see cref="Push.LogPushSender"/>.
/// </summary>
internal sealed class LogEmailSender(ILogger<LogEmailSender> logger) : IEmailSender
{
    public Task<Result> SendAsync(
        string toEmail, string subject, string body, CancellationToken ct, EmailAttachment? attachment = null)
    {
        logger.LogInformation(
            "Email (log-only, SMTP not configured) to {ToEmail}: {Subject}", toEmail, subject);
        if (attachment is not null)
            logger.LogInformation(
                "Email attachment: {FileName} ({Length} bytes)", attachment.FileName, attachment.Content.Length);
        return Task.FromResult(Result.Success());
    }
}
