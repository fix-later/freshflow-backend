namespace FreshFlow.Notifications.Infrastructure.Email;

/// <summary>
/// SMTP settings bound from <c>Notifications:Email:Smtp</c>. Host + FromAddress present ⇒ SMTP
/// is used; otherwise the app falls back to <see cref="LogEmailSender"/>. Credentials must come
/// from environment/user-secrets, never from source-controlled appsettings.
/// </summary>
internal sealed class SmtpEmailOptions
{
    public string? Host { get; init; }
    public int Port { get; init; } = 587;
    public bool EnableSsl { get; init; } = true;
    public string? FromAddress { get; init; }
    public string? Username { get; init; }
    public string? Password { get; init; }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(Host) && !string.IsNullOrWhiteSpace(FromAddress);
}
