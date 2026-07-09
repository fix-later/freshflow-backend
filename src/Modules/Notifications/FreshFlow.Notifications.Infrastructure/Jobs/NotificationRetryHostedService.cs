using FreshFlow.Notifications.Application.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FreshFlow.Notifications.Infrastructure.Jobs;

internal sealed class NotificationRetryHostedService(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<NotificationRetryHostedService> logger) : BackgroundService
{
    private const int DefaultIntervalSeconds = 60;
    private const int MinimumIntervalSeconds = 10;
    private const int DefaultMaxAttempts = 5;
    private const int DefaultBackoffSeconds = 60;
    private const int DefaultBatchSize = 50;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!ReadBool("Notifications:Retry:Enabled", defaultValue: true))
        {
            logger.LogInformation("Notification retry job is disabled.");
            return;
        }

        await RetryOnceAsync(stoppingToken);

        var intervalSeconds = Math.Max(
            MinimumIntervalSeconds,
            ReadInt("Notifications:Retry:IntervalSeconds", DefaultIntervalSeconds));

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(intervalSeconds));
        while (await timer.WaitForNextTickAsync(stoppingToken))
            await RetryOnceAsync(stoppingToken);
    }

    private async Task RetryOnceAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var retryService = scope.ServiceProvider.GetRequiredService<INotificationRetryService>();
            var processed = await retryService.RetryDueAsync(
                ReadInt("Notifications:Retry:MaxAttempts", DefaultMaxAttempts),
                TimeSpan.FromSeconds(ReadInt("Notifications:Retry:BackoffSeconds", DefaultBackoffSeconds)),
                ReadInt("Notifications:Retry:BatchSize", DefaultBatchSize),
                stoppingToken);

            if (processed > 0)
            {
                logger.LogInformation(
                    "Retried {NotificationCount} failed notification deliveries.",
                    processed);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Notification retry job failed.");
        }
    }

    private bool ReadBool(string key, bool defaultValue) =>
        bool.TryParse(configuration[key], out var value) ? value : defaultValue;

    private int ReadInt(string key, int defaultValue) =>
        int.TryParse(configuration[key], out var value) ? value : defaultValue;
}
