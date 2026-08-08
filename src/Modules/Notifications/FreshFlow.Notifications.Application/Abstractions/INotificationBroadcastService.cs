using FreshFlow.Notifications.Application.Dtos;

namespace FreshFlow.Notifications.Application.Abstractions;

public interface INotificationBroadcastService
{
    public Task BroadcastCreatedAsync(
        Guid userId,
        NotificationDto notification,
        CancellationToken ct);
}
