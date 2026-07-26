using FreshFlow.Invoicing.Application.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FreshFlow.Invoicing.Infrastructure.Jobs;

/// <summary>
/// Periodically re-attempts issuance for invoices still awaiting a tax-authority code (a provider
/// outage at delivery time leaves them PendingIssuance). Mirrors NotificationRetryHostedService.
/// </summary>
internal sealed class InvoiceIssuanceRetryHostedService(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<InvoiceIssuanceRetryHostedService> logger) : BackgroundService
{
    private const int DefaultIntervalSeconds = 120;
    private const int MinimumIntervalSeconds = 10;
    private const int DefaultMaxAttempts = 5;
    private const int DefaultBackoffSeconds = 120;
    private const int DefaultBatchSize = 50;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!ReadBool("Invoicing:Retry:Enabled", defaultValue: true))
        {
            logger.LogInformation("Invoice issuance retry job is disabled.");
            return;
        }

        await RetryOnceAsync(stoppingToken);

        var intervalSeconds = Math.Max(
            MinimumIntervalSeconds,
            ReadInt("Invoicing:Retry:IntervalSeconds", DefaultIntervalSeconds));

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(intervalSeconds));
        while (await timer.WaitForNextTickAsync(stoppingToken))
            await RetryOnceAsync(stoppingToken);
    }

    private async Task RetryOnceAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var issuance = scope.ServiceProvider.GetRequiredService<IInvoiceIssuanceService>();
            var processed = await issuance.RetryDueAsync(
                Math.Max(2, ReadInt("Invoicing:Retry:MaxAttempts", DefaultMaxAttempts)),
                TimeSpan.FromSeconds(ReadInt("Invoicing:Retry:BackoffSeconds", DefaultBackoffSeconds)),
                ReadInt("Invoicing:Retry:BatchSize", DefaultBatchSize),
                stoppingToken);

            if (processed > 0)
                logger.LogInformation("Re-attempted issuance for {Count} pending invoices.", processed);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Invoice issuance retry job failed.");
        }
    }

    private bool ReadBool(string key, bool defaultValue) =>
        bool.TryParse(configuration[key], out var value) ? value : defaultValue;

    private int ReadInt(string key, int defaultValue) =>
        int.TryParse(configuration[key], out var value) ? value : defaultValue;
}
