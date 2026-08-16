using FreshFlow.Orders.Application.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FreshFlow.Orders.Infrastructure.Jobs;

internal sealed class ScheduledOrderGenerationHostedService(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<ScheduledOrderGenerationHostedService> logger) : BackgroundService
{
    private const int DefaultIntervalSeconds = 60;
    private const int MinimumIntervalSeconds = 10;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!ReadBool("Orders:ScheduledOrderGeneration:Enabled", defaultValue: true))
        {
            logger.LogInformation("Scheduled order generation job is disabled.");
            return;
        }

        await GenerateOnceAsync(stoppingToken);

        var intervalSeconds = Math.Max(
            MinimumIntervalSeconds,
            ReadInt("Orders:ScheduledOrderGeneration:IntervalSeconds", DefaultIntervalSeconds));

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(intervalSeconds));
        while (await timer.WaitForNextTickAsync(stoppingToken))
            await GenerateOnceAsync(stoppingToken);
    }

    private async Task GenerateOnceAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var generator = scope.ServiceProvider.GetRequiredService<IScheduledOrderGenerationService>();
            var result = await generator.GenerateDueAsync(DateTime.UtcNow, stoppingToken);

            if (result.MissedExecutionCount > 0)
            {
                logger.LogWarning(
                    "MISSED_EXECUTION: recovered {MissedExecutionCount} missed recurring order executions.",
                    result.MissedExecutionCount);
            }

            if (result.CreatedOrderCount > 0)
            {
                logger.LogInformation(
                    "Generated {CreatedOrderCount} recurring order instances from {ScheduledOrderCount} schedules.",
                    result.CreatedOrderCount,
                    result.ScheduledOrderCount);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Scheduled order generation job failed.");
        }
    }

    private bool ReadBool(string key, bool defaultValue) =>
        bool.TryParse(configuration[key], out var value) ? value : defaultValue;

    private int ReadInt(string key, int defaultValue) =>
        int.TryParse(configuration[key], out var value) ? value : defaultValue;
}
