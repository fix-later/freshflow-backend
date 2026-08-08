using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace FreshFlow.Notifications.Infrastructure.Realtime;

[Authorize]
public sealed class NotificationHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        if (!Guid.TryParse(Context.User?.FindFirst("sub")?.Value, out var userId))
            throw new HubException("Unable to determine caller identity.");

        await Groups.AddToGroupAsync(
            Context.ConnectionId,
            UserGroupName(userId),
            Context.ConnectionAborted);
        await base.OnConnectedAsync();
    }

    internal static string UserGroupName(Guid userId) => $"user:{userId}";
}
