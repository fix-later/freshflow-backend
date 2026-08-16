using FreshFlow.Notifications.Application.Abstractions;
using FreshFlow.Notifications.Domain.Entities;
using FreshFlow.SharedKernel.Application;
using Microsoft.Extensions.Logging;

namespace FreshFlow.Notifications.Infrastructure.Push;

internal sealed class LogPushSender(ILogger<LogPushSender> logger) : IPushSender
{
    public Task<Result> SendAsync(Notification notification, CancellationToken ct)
    {
        // Real FCM/APNs integration is a follow-up outside this epic.
        logger.LogInformation(
            "Notification {NotificationId} prepared for push delivery to user {UserId}.",
            notification.Id,
            notification.UserId);

        return Task.FromResult(Result.Success());
    }
}
