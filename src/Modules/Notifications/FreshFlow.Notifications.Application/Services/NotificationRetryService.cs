using FreshFlow.Notifications.Application.Abstractions;

namespace FreshFlow.Notifications.Application.Services;

public sealed class NotificationRetryService(
    INotificationRepository notifications,
    IPushSender pushSender) : INotificationRetryService
{
    public async Task<int> RetryDueAsync(
        int maxAttempts,
        TimeSpan backoff,
        int batchSize,
        CancellationToken ct)
    {
        var backoffThreshold = DateTime.UtcNow.Subtract(backoff);
        var dueNotifications = await notifications.GetRetryablePageAsync(
            maxAttempts,
            backoffThreshold,
            batchSize,
            ct);

        var processed = 0;
        foreach (var notification in dueNotifications)
        {
            try
            {
                try
                {
                    var result = await pushSender.SendAsync(notification, ct);
                    if (result.IsSuccess)
                        notification.MarkSent();
                    else
                        notification.MarkFailed(
                            string.IsNullOrWhiteSpace(result.Error.Message) ? "Unknown error" : result.Error.Message);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    notification.MarkFailed(ex.Message);
                }

                await notifications.UpdateAsync(notification, ct);
                processed++;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
                // Each retry item is isolated so one bad row does not block the batch.
            }
        }

        return processed;
    }
}
