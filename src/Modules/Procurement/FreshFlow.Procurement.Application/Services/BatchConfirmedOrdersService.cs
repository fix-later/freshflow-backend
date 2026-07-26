using FreshFlow.Procurement.Application.Abstractions;
using FreshFlow.Procurement.Application.Dtos;
using FreshFlow.Procurement.Domain.Entities;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Procurement.Application.Services;

public sealed class BatchConfirmedOrdersService(
    IConfirmedOrderReader orders,
    IMarketProductMarketReader marketProducts,
    IHubByMarketReader hubsByMarket,
    IOperationalSettingsReader settings,
    IProcurementBatchRepository batches) : IProcurementBatchingService
{
    public async Task<Result<BatchingResult>> BuildBatchesAsync(
        DateOnly batchDate,
        bool dryRun,
        bool force,
        CancellationToken ct)
    {
        var operationalSettings = await settings.ReadAsync(ct);
        if (!operationalSettings.BatchingEnabled)
            return Skipped("batching_disabled");

        var eligibleOrders = await orders.ReadEligibleAsync(batchDate, force, ct);
        if (eligibleOrders.Count == 0)
            return Skipped("no_eligible_orders");

        var marketProductIds = eligibleOrders
            .SelectMany(order => order.Items)
            .Select(item => item.MarketProductId)
            .Distinct()
            .ToArray();
        var markets = await marketProducts.ReadMarketsAsync(marketProductIds, ct);
        var missingMarketProductId = marketProductIds.FirstOrDefault(id => !markets.ContainsKey(id));
        if (missingMarketProductId != Guid.Empty)
        {
            return Result<BatchingResult>.Failure(Error.Validation(
                "MARKET_PRODUCT_NOT_FOUND",
                $"Market product '{missingMarketProductId}' is unavailable for batching."));
        }

        var lines = eligibleOrders
            .SelectMany(order => order.Items.Select(item => new
            {
                OrderId = order.Id,
                item.MarketProductId,
                item.ProductNameSnapshot,
                item.Quantity,
                MarketId = markets[item.MarketProductId]
            }))
            .ToList();

        if (lines.Count == 0)
        {
            return Result<BatchingResult>.Failure(Error.Validation(
                "INVALID_PROCUREMENT_BATCH",
                "Eligible orders must contain at least one item."));
        }

        var marketIds = lines.Select(line => line.MarketId).Distinct().ToArray();
        var hubs = await hubsByMarket.ReadActiveHubsAsync(marketIds, ct);
        var marketWithoutHub = marketIds.FirstOrDefault(marketId => !hubs.ContainsKey(marketId));
        if (marketWithoutHub != Guid.Empty)
        {
            return Result<BatchingResult>.Failure(Error.Validation(
                "HUB_NOT_CONFIGURED_FOR_MARKET",
                $"Market '{marketWithoutHub}' has no active hub."));
        }

        var builtBatches = new List<ProcurementBatch>();
        foreach (var marketGroup in lines.GroupBy(line => line.MarketId))
        {
            var build = ProcurementBatch.Build(
                batchDate,
                marketGroup.Key,
                marketGroup.Select(line => (
                    line.MarketProductId,
                    line.ProductNameSnapshot,
                    line.Quantity,
                    line.OrderId)),
                hubs[marketGroup.Key]);

            if (build.IsFailure)
                return Result<BatchingResult>.Failure(build.Error);

            builtBatches.Add(build.Value);
        }

        var preview = builtBatches.Select(ToPreview).ToList().AsReadOnly();
        var distinctOrderCount = builtBatches
            .SelectMany(batch => batch.Orders)
            .Select(link => link.OrderId)
            .Distinct()
            .Count();
        var itemCount = builtBatches.Sum(batch => batch.TotalItemCount);

        if (dryRun)
        {
            return Result<BatchingResult>.Success(new BatchingResult(
                0,
                0,
                itemCount,
                false,
                null,
                preview));
        }

        await batches.AddRangeAsync(builtBatches.AsReadOnly(), ct);
        if (!await batches.SaveChangesAsync(ct))
        {
            return Result<BatchingResult>.Failure(Error.Conflict(
                "ORDER_ALREADY_IN_ACTIVE_GROUP",
                "An order already belongs to an active procurement batch."));
        }

        return Result<BatchingResult>.Success(new BatchingResult(
            builtBatches.Count,
            distinctOrderCount,
            itemCount,
            false,
            null,
            preview));
    }

    private static Result<BatchingResult> Skipped(string reason) =>
        Result<BatchingResult>.Success(new BatchingResult(
            0,
            0,
            0,
            true,
            reason,
            []));

    private static BatchingPreviewDto ToPreview(ProcurementBatch batch) =>
        new(
            batch.BatchDate,
            batch.MarketId,
            batch.Status.ToString(),
            batch.Orders.Select(link => link.OrderId).ToList().AsReadOnly(),
            batch.Items
                .Select(item => new BatchingPreviewItemDto(
                    item.MarketProductId,
                    item.ProductNameSnapshot,
                    item.TotalQuantity))
                .ToList()
                .AsReadOnly());
}
