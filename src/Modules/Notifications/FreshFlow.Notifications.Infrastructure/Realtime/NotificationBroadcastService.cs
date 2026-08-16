using FreshFlow.Notifications.Application.Abstractions;
using FreshFlow.Notifications.Application.Dtos;
using Microsoft.AspNetCore.SignalR;

namespace FreshFlow.Notifications.Infrastructure.Realtime;

internal sealed class NotificationBroadcastService(IHubContext<NotificationHub> hubContext)
    : INotificationBroadcastService
{
    private const string NotificationCreatedMethod = "NotificationCreated";

    public async Task BroadcastCreatedAsync(
        Guid userId,
        NotificationDto notification,
        CancellationToken ct)
    {
        await hubContext.Clients
            .Group(NotificationHub.UserGroupName(userId))
            .SendAsync(NotificationCreatedMethod, notification, ct);
    }
}
