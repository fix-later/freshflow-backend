using FreshFlow.Hub.Application.Abstractions;
using FreshFlow.Hub.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FreshFlow.Hub.Infrastructure.Jobs;

/// <summary>
/// One-shot startup backfill: creates PENDING HubInboundEvents for procurement batches that were
/// handed off before Task 1's auto-create handler existed. Idempotent — safe to run every boot.
/// </summary>
internal sealed class HubInboundBackfillHostedService(
    IServiceScopeFactory scopeFactory,
    ILogger<HubInboundBackfillHostedService> logger) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        RunBackfillAsync(stoppingToken);

    // Exposed for direct unit testing — bypasses BackgroundService's Start/Stop lifecycle timing.
    internal async Task RunBackfillAsync(CancellationToken stoppingToken)
    {
        using var scope = scopeFactory.CreateScope();
        var procurement = scope.ServiceProvider.GetRequiredService<IHubProcurementPlanReader>();
        var inbounds = scope.ServiceProvider.GetRequiredService<IHubInboundRepository>();

        var batches = await procurement.ReadHandedOffBatchesAsync(stoppingToken);
        var created = 0;

        // ponytail: sequential per-batch loop — backfill volume is small (one-time catch-up),
        // parallelize only if this ever needs to run against a large historical backlog.
        foreach (var batch in batches)
        {
            try
            {
                if (await inbounds.DeliveryScheduleExistsAsync(batch.HubId, batch.BatchId, stoppingToken))
                    continue;

                var items = await procurement.ReadBatchItemsAsync(batch.BatchId, stoppingToken);
                var inboundItems = items
                    .Where(item => item.ActualQuantity is > 0)
                    .Select(item => new HubInboundItem(
                        item.MarketProductId,
                        ProductId: null,
                        QuantityKg: item.ActualQuantity!.Value,
                        ProductName: item.ProductName))
                    .ToList()
                    .AsReadOnly();

                if (inboundItems.Count == 0)
                    continue;

                var inbound = HubInboundEvent.Record(
                    batch.HubId,
                    sourceMarketId: batch.MarketId,
                    deliveryRouteId: null,
                    deliveryScheduleId: batch.BatchId,
                    inboundItems,
                    arrivedAt: batch.HandedOffAt,
                    recordedBy: batch.AssignedAgentUserId,
                    hubStaffUserId: null);

                await inbounds.AddAsync(inbound, stoppingToken);
                await inbounds.SaveChangesAsync(stoppingToken);
                created++;
            }
            catch (HubConcurrencyException)
            {
                // Already recorded by a concurrent run/handover — not an error.
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(
                    ex,
                    "Failed to backfill hub inbound event for BatchId={BatchId}, HubId={HubId}.",
                    batch.BatchId,
                    batch.HubId);
            }
        }

        logger.LogInformation(
            "Hub inbound backfill created {Created} inbound event(s) from {BatchCount} handed-off batch(es).",
            created,
            batches.Count);
    }
}
