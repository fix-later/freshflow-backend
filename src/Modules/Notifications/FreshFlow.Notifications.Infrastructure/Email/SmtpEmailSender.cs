using System.Net;
using System.Net.Mail;
using FreshFlow.Notifications.Application.Abstractions;
using FreshFlow.SharedKernel.Application;
using Microsoft.Extensions.Logging;

namespace FreshFlow.Notifications.Infrastructure.Email;

/// <summary>
/// SMTP <see cref="IEmailSender"/> over the framework's <see cref="SmtpClient"/> — no extra
/// NuGet dependency. Never throws: a delivery failure is logged and returned as a failed
/// <see cref="Result"/> so a bad email never aborts statement generation or notification write.
/// </summary>
// ponytail: System.Net.Mail.SmtpClient is fine for low-volume monthly sends; swap to MailKit
// only if modern TLS / OAuth2 auth becomes a requirement.
internal sealed class SmtpEmailSender(SmtpEmailOptions options, ILogger<SmtpEmailSender> logger) : IEmailSender
{
    public async Task<Result> SendAsync(string toEmail, string subject, string body, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(toEmail))
            return Result.Failure(Error.Validation("EMAIL_RECIPIENT_MISSING", "No recipient email address."));

        try
        {
            using var message = new MailMessage(options.FromAddress!, toEmail, subject, body);
            using var client = new SmtpClient(options.Host!, options.Port)
            {
                EnableSsl = options.EnableSsl,
                Credentials = string.IsNullOrWhiteSpace(options.Username)
                    ? CredentialCache.DefaultNetworkCredentials
                    : new NetworkCredential(options.Username, options.Password),
            };

            await client.SendMailAsync(message, ct);
            return Result.Success();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Failed to send email to {ToEmail}.", toEmail);
            return Result.Failure(Error.Validation("EMAIL_SEND_FAILED", ex.Message));
        }
    }
}
