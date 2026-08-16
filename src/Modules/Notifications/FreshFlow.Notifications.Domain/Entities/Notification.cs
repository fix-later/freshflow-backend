using FreshFlow.Notifications.Domain.Enums;

namespace FreshFlow.Notifications.Domain.Entities;

public sealed class Notification
{
    private Notification() { } // EF Core

    public Notification(
        Guid userId,
        NotificationType type,
        string title,
        string body,
        string? payload)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("User id is required.", nameof(userId));

        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title is required.", nameof(title));

        if (string.IsNullOrWhiteSpace(body))
            throw new ArgumentException("Body is required.", nameof(body));

        Id = Guid.NewGuid();
        UserId = userId;
        Type = type;
        Title = title.Trim();
        Body = body.Trim();
        Payload = string.IsNullOrWhiteSpace(payload) ? null : payload;
        SendStatus = NotificationSendStatus.pending;
        AttemptCount = 0;
        CreatedAt = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public NotificationType Type { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Body { get; private set; } = string.Empty;
    public string? Payload { get; private set; }
    public bool IsRead { get; private set; }
    public DateTime? ReadAt { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public NotificationSendStatus SendStatus { get; private set; } = NotificationSendStatus.pending;
    public int AttemptCount { get; private set; }
    public DateTime? LastAttemptAt { get; private set; }
    public string? FailedReason { get; private set; }
    public DateTime? DeletedAt { get; private set; }

    public void MarkRead()
    {
        if (IsRead)
            return;

        IsRead = true;
        ReadAt = DateTime.UtcNow;
    }

    public void MarkSent()
    {
        SendStatus = NotificationSendStatus.sent;
        AttemptCount++;
        LastAttemptAt = DateTime.UtcNow;
        FailedReason = null;
    }

    public void MarkFailed(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Failure reason is required.", nameof(reason));

        SendStatus = NotificationSendStatus.failed;
        AttemptCount++;
        LastAttemptAt = DateTime.UtcNow;
        FailedReason = reason;
    }
}
