using FreshFlow.Procurement.Application.Abstractions;
using FreshFlow.Procurement.Application.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FreshFlow.Procurement.Infrastructure.Jobs;

internal sealed class ProcurementBatchingHostedService(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    TimeProvider timeProvider,
    ILogger<ProcurementBatchingHostedService> logger) : BackgroundService
{
    private const int DefaultIntervalSeconds = 60;
    private const int MinimumIntervalSeconds = 10;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!ReadBool("Procurement:Batching:Enabled", true))
        {
            logger.LogInformation("Procurement batching job is disabled.");
            return;
        }

        await BuildOnceAsync(stoppingToken);

        var intervalSeconds = Math.Max(
            MinimumIntervalSeconds,
            ReadInt("Procurement:Batching:IntervalSeconds", DefaultIntervalSeconds));
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(intervalSeconds));

        while (await timer.WaitForNextTickAsync(stoppingToken))
            await BuildOnceAsync(stoppingToken);
    }

    private async Task BuildOnceAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var settings = await scope.ServiceProvider
                .GetRequiredService<IOperationalSettingsReader>()
                .ReadAsync(stoppingToken);
            if (!settings.BatchingEnabled)
                return;

            var dueDate = ProcurementBatchCycle.ResolveDueBatchDate(
                timeProvider.GetUtcNow(),
                settings.DailyCutoffTime);
            var cycleDate = await scope.ServiceProvider
                .GetRequiredService<IConfirmedOrderReader>()
                .FindOldestEligibleCycleAsync(dueDate, stoppingToken);
            if (!cycleDate.HasValue)
                return;

            var repository = scope.ServiceProvider
                .GetRequiredService<IProcurementBatchRepository>();
            if (await repository.CycleExistsAsync(cycleDate.Value, stoppingToken))
                return;

            var result = await scope.ServiceProvider
                .GetRequiredService<IProcurementBatchingService>()
                .BuildBatchesAsync(cycleDate.Value, false, false, stoppingToken);
            if (result.IsFailure)
            {
                logger.LogWarning(
                    "Procurement batching failed for cycle {BatchDate}: {ErrorCode}.",
                    cycleDate,
                    result.Error.Code);
                return;
            }

            if (cycleDate.Value < dueDate)
            {
                logger.LogWarning(
                    "MISSED_EXECUTION: recovered procurement batching cycle {BatchDate}.",
                    cycleDate);
            }

            if (result.Value.BatchesCreated > 0)
            {
                logger.LogInformation(
                    "Built {BatchCount} procurement batches for {OrderCount} orders in cycle {BatchDate}.",
                    result.Value.BatchesCreated,
                    result.Value.OrdersBatched,
                    cycleDate);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Procurement batching job failed.");
        }
    }

    private bool ReadBool(string key, bool defaultValue) =>
        bool.TryParse(configuration[key], out var value) ? value : defaultValue;

    private int ReadInt(string key, int defaultValue) =>
        int.TryParse(configuration[key], out var value) ? value : defaultValue;
}
