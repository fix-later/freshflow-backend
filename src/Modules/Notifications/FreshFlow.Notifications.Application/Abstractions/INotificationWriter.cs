using FreshFlow.Notifications.Domain.Entities;
using FreshFlow.Notifications.Domain.Enums;

namespace FreshFlow.Notifications.Application.Abstractions;

public interface INotificationWriter
{
    public Task<Notification> WriteAsync(
        Guid userId,
        NotificationType type,
        string title,
        string body,
        IReadOnlyDictionary<string, object?>? payload,
        CancellationToken ct);
}
