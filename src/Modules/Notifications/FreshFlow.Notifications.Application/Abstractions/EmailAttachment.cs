namespace FreshFlow.Notifications.Application.Abstractions;

/// <summary>A single file attachment for <see cref="IEmailSender.SendAsync"/>.</summary>
public sealed record EmailAttachment(string FileName, byte[] Content, string ContentType);
