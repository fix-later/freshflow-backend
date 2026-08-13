using FreshFlow.Procurement.Application.Abstractions;
using FreshFlow.Procurement.Application.Common;
using FreshFlow.Procurement.Application.Dtos;
using FreshFlow.Procurement.Domain.Entities;
using FreshFlow.Procurement.Domain.Enums;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Procurement.Application.Services;

public sealed class BatchConfirmedOrdersService(
    IConfirmedOrderReader orders,
    IMarketProductMarketReader marketProducts,
    IHubByMarketReader hubsByMarket,
    IMarketCodeReader marketCodes,
    IOperationalSettingsReader settings,
    IProcurementBatchRepository batches,
    TimeProvider timeProvider,
    IMarketSessionRepository? sessions = null) : IProcurementBatchingService
{
    public async Task<Result<BatchingResult>> BuildSessionBatchAsync(
        Guid marketSessionId,
        bool dryRun,
        CancellationToken ct)
    {
        if (sessions is null)
            return Result<BatchingResult>.Failure(Error.Conflict(
                "MARKET_SESSION_NOT_AVAILABLE", "Market-session batching is not configured."));

        var session = await sessions.FindByIdAsync(marketSessionId, ct);
        if (session is null)
            return Result<BatchingResult>.Failure(Error.NotFound("MARKET_SESSION", marketSessionId));
        if (session.Status != MarketSessionStatus.Closed)
            return Result<BatchingResult>.Failure(Error.Conflict(
                "MARKET_SESSION_NOT_CLOSED", "Only a closed market session can be batched."));
        if (session.BatchingCompletedAt.HasValue)
            return Skipped("already_completed");
        var operational = await settings.ReadAsync(ct);
        if (!operational.BatchingEnabled)
            return Skipped("batching_disabled");

        var eligibleOrders = await orders.ReadEligibleForSessionAsync(session.Id, ct);
        if (eligibleOrders.Count == 0)
        {
            if (!dryRun)
            {
                session.MarkBatchingCompleted(timeProvider.GetUtcNow().UtcDateTime);
                await sessions.SaveChangesAsync(ct);
            }
            return Skipped("no_eligible_orders");
        }
        if (session.HubId is null)
            return Result<BatchingResult>.Failure(Error.Validation(
                "HUB_NOT_CONFIGURED_FOR_MARKET", "The market session has no hub."));

        var lines = eligibleOrders.SelectMany(order => order.Items.Select(item => (
            item.MarketProductId, item.ProductNameSnapshot, item.Quantity, OrderId: order.Id))).ToList();
        var market = (await marketCodes.ReadMarketCodesAsync([session.MarketId], ct))
            .GetValueOrDefault(session.MarketId);
        if (string.IsNullOrWhiteSpace(market.Name))
            return Result<BatchingResult>.Failure(Error.NotFound("MARKET", session.MarketId));
        var sequence = await batches.CountByMarketAndDateAsync(
            session.MarketId, session.ServiceDate, ct) + 1;
        var code = $"{market.Code ?? MarketCode.DeriveMarketCode(market.Name)}-{session.ServiceDate:yyMMdd}-{sequence}";
        var build = ProcurementBatch.Build(
            session.ServiceDate, session.MarketId, lines, session.HubId.Value, code, session.Id);
        if (build.IsFailure)
            return Result<BatchingResult>.Failure(build.Error);

        if (dryRun)
            return Result<BatchingResult>.Success(new BatchingResult(
                0, 0, build.Value.Items.Count, false, null, [ToPreview(build.Value)]));

        session.MarkBatchingCompleted(timeProvider.GetUtcNow().UtcDateTime);
        await batches.AddRangeAsync([build.Value], ct);
        if (!await batches.SaveChangesAsync(ct))
            return Result<BatchingResult>.Failure(Error.Conflict(
                "ORDER_ALREADY_IN_ACTIVE_GROUP", "The session or an order already has an active procurement batch."));

        return Result<BatchingResult>.Success(new BatchingResult(
            1, eligibleOrders.Count, build.Value.Items.Count, false, null, [ToPreview(build.Value)]));
    }

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

        var marketDetails = await marketCodes.ReadMarketCodesAsync(marketIds, ct);
        var missingMarketId = marketIds.FirstOrDefault(marketId => !marketDetails.ContainsKey(marketId));
        if (missingMarketId != Guid.Empty)
        {
            return Result<BatchingResult>.Failure(
                Error.NotFound("Market", missingMarketId));
        }

        var marketGroups = lines.GroupBy(line => line.MarketId).ToList();
        var previewItemCount = marketGroups.Sum(group =>
            group.Select(line => line.MarketProductId).Distinct().Count());

        if (dryRun)
        {
            var previewBatches = new List<ProcurementBatch>();
            foreach (var marketGroup in marketGroups)
            {
                var build = Build(
                    marketGroup.Key,
                    marketGroup.Select(line => (
                        line.MarketProductId,
                        line.ProductNameSnapshot,
                        line.Quantity,
                        line.OrderId)),
                    "(preview)");
                if (build.IsFailure)
                    return Result<BatchingResult>.Failure(build.Error);
                previewBatches.Add(build.Value);
            }

            return Result<BatchingResult>.Success(new BatchingResult(
                0,
                0,
                previewItemCount,
                false,
                null,
                previewBatches.Select(ToPreview).ToList().AsReadOnly()));
        }

        var mergeable = await batches.ListMergeableByDateAsync(batchDate, ct);
        var mergeByMarket = mergeable
            .GroupBy(batch => batch.MarketId)
            .ToDictionary(group => group.Key, group => group.First());
        var newBatches = new List<ProcurementBatch>();
        var affectedBatches = new List<ProcurementBatch>();
        var batchedOrderIds = new HashSet<Guid>();
        var itemsAggregated = 0;
        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;

        foreach (var marketGroup in marketGroups)
        {
            if (!mergeByMarket.TryGetValue(marketGroup.Key, out var existing))
            {
                var market = marketDetails[marketGroup.Key];
                var marketCode = market.Code ?? MarketCode.DeriveMarketCode(market.Name);
                var sequence = await batches.CountByMarketAndDateAsync(marketGroup.Key, batchDate, ct) + 1;
                var code = $"{marketCode}-{batchDate:yyMMdd}-{sequence}";
                var build = Build(
                    marketGroup.Key,
                    marketGroup.Select(line => (
                        line.MarketProductId,
                        line.ProductNameSnapshot,
                        line.Quantity,
                        line.OrderId)),
                    code);
                if (build.IsFailure)
                    return Result<BatchingResult>.Failure(build.Error);
                newBatches.Add(build.Value);
                affectedBatches.Add(build.Value);
                batchedOrderIds.UnionWith(marketGroup.Select(line => line.OrderId));
                itemsAggregated += marketGroup
                    .Select(line => line.MarketProductId)
                    .Distinct()
                    .Count();
                continue;
            }

            var existingOrderIds = existing.Orders
                .Select(order => order.OrderId)
                .ToHashSet();
            var newOrderIds = marketGroup
                .Select(line => line.OrderId)
                .Distinct()
                .Where(orderId => !existingOrderIds.Contains(orderId))
                .ToHashSet();
            batchedOrderIds.UnionWith(newOrderIds);
            itemsAggregated += marketGroup
                .Where(line => newOrderIds.Contains(line.OrderId))
                .Select(line => line.MarketProductId)
                .Distinct()
                .Count();

            IReadOnlyDictionary<Guid, decimal> prices = new Dictionary<Guid, decimal>();
            if (existing.Status == ProcurementBatchStatus.Manifested)
            {
                var existingProductIds = existing.Items
                    .Select(item => item.MarketProductId)
                    .ToHashSet();
                var newProductIds = marketGroup
                    .Where(line => newOrderIds.Contains(line.OrderId))
                    .Select(line => line.MarketProductId)
                    .Distinct()
                    .Where(productId => !existingProductIds.Contains(productId))
                    .ToArray();
                if (newProductIds.Length > 0)
                {
                    prices = await marketProducts.ReadReferencePricesAsync(newProductIds, ct);
                }
            }

            var merge = existing.MergeIn(
                marketGroup.Select(line => (
                    line.MarketProductId,
                    line.ProductNameSnapshot,
                    line.Quantity,
                    line.OrderId)),
                prices,
                nowUtc);
            if (merge.IsFailure)
                return Result<BatchingResult>.Failure(merge.Error);
            affectedBatches.Add(existing);
        }

        if (newBatches.Count > 0)
            await batches.AddRangeAsync(newBatches.AsReadOnly(), ct);
        if (!await batches.SaveChangesAsync(ct))
        {
            return Result<BatchingResult>.Failure(Error.Conflict(
                "ORDER_ALREADY_IN_ACTIVE_GROUP",
                "An order already belongs to an active procurement batch."));
        }

        return Result<BatchingResult>.Success(new BatchingResult(
            newBatches.Count,
            batchedOrderIds.Count,
            itemsAggregated,
            false,
            null,
            affectedBatches.Select(ToPreview).ToList().AsReadOnly()));

        Result<ProcurementBatch> Build(
            Guid marketId,
            IEnumerable<(Guid MarketProductId, string ProductName, int Quantity, Guid OrderId)> groupLines,
            string code) =>
            ProcurementBatch.Build(
                batchDate,
                marketId,
                groupLines,
                hubs[marketId],
                code);
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
            batch.Code,
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
