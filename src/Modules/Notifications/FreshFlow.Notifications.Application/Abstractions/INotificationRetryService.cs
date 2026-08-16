namespace FreshFlow.Notifications.Application.Abstractions;

public interface INotificationRetryService
{
    public Task<int> RetryDueAsync(
        int maxAttempts,
        TimeSpan backoff,
        int batchSize,
        CancellationToken ct);
}
