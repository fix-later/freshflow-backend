using FreshFlow.Procurement.Application.Abstractions;
using FreshFlow.Procurement.Application.Common;
using FreshFlow.Procurement.Application.Dtos;
using FreshFlow.Procurement.Domain.Entities;
using FreshFlow.Procurement.Domain.Enums;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Procurement.Application.Services;

public sealed class BatchConfirmedOrdersService(
    IConfirmedOrderReader orders,
    IMarketCodeReader marketCodes,
    IOperationalSettingsReader settings,
    IProcurementBatchRepository batches,
    TimeProvider timeProvider,
    IMarketSessionRepository sessions) : IProcurementBatchingService
{
    public async Task<Result<BatchingResult>> BuildSessionBatchAsync(
        Guid marketSessionId,
        bool dryRun,
        CancellationToken ct)
    {
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
