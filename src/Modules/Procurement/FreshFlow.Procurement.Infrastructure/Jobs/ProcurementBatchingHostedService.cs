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
            var lifecycle = scope.ServiceProvider.GetRequiredService<MarketSessionLifecycleService>();
            await lifecycle.EnsureRollingWindowAsync(stoppingToken);
            await lifecycle.CloseDueAsync(stoppingToken);

            var settings = await scope.ServiceProvider
                .GetRequiredService<IOperationalSettingsReader>()
                .ReadAsync(stoppingToken);
            if (!settings.BatchingEnabled)
                return;

            var pending = await scope.ServiceProvider
                .GetRequiredService<IMarketSessionRepository>()
                .ListClosedPendingAsync(stoppingToken);
            var batching = scope.ServiceProvider.GetRequiredService<IProcurementBatchingService>();
            foreach (var session in pending)
            {
                var result = await batching.BuildSessionBatchAsync(session.Id, false, stoppingToken);
                if (result.IsFailure)
                    logger.LogWarning(
                        "Procurement batching failed for session {SessionId}: {ErrorCode}.",
                        session.Id,
                        result.Error.Code);
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
