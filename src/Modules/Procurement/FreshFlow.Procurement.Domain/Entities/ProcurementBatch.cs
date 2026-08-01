using FreshFlow.Procurement.Domain.Enums;
using FreshFlow.Procurement.Domain.Events;
using FreshFlow.SharedKernel.Application;
using FreshFlow.SharedKernel.Domain;

namespace FreshFlow.Procurement.Domain.Entities;

public sealed class ProcurementBatch : AggregateRoot
{
    private readonly List<ProcurementBatchItem> _items = [];
    private readonly List<ProcurementBatchOrder> _orders = [];
    private readonly List<ProcurementException> _exceptions = [];

    private ProcurementBatch() { }

    public DateOnly BatchDate { get; private set; }
    public Guid MarketId { get; private set; }
    public ProcurementBatchStatus Status { get; private set; }
    public DateTime? ManifestedAt { get; private set; }
    public Guid? AssignedAgentUserId { get; private set; }
    public DateTime? AssignedAt { get; private set; }
    public DateTime? HandedOffAt { get; private set; }
    public Guid? HubId { get; private set; }
    public int TotalItemCount { get; private set; }
    public DateTime? CancelledAt { get; private set; }
    public string? CancellationReason { get; private set; }

    public IReadOnlyCollection<ProcurementBatchItem> Items => _items.AsReadOnly();
    public IReadOnlyCollection<ProcurementBatchOrder> Orders => _orders.AsReadOnly();
    public IReadOnlyCollection<ProcurementException> Exceptions => _exceptions.AsReadOnly();

    public static Result<ProcurementBatch> Build(
        DateOnly batchDate,
        Guid marketId,
        IEnumerable<(Guid MarketProductId, string ProductName, int Quantity, Guid OrderId)> lines,
        Guid hubId)
    {
        var input = lines?.ToList() ?? [];

        if (batchDate == default || marketId == Guid.Empty || hubId == Guid.Empty || input.Count == 0 ||
            input.Any(line =>
                line.MarketProductId == Guid.Empty ||
                line.OrderId == Guid.Empty ||
                string.IsNullOrWhiteSpace(line.ProductName) ||
                line.Quantity <= 0))
        {
            return Result<ProcurementBatch>.Failure(Error.Validation(
                "INVALID_PROCUREMENT_BATCH",
                "A procurement batch requires a date, market, and positive product lines."));
        }

        var aggregated = input
            .GroupBy(line => line.MarketProductId)
            .Select(group => new
            {
                MarketProductId = group.Key,
                ProductName = group.First().ProductName.Trim(),
                TotalQuantity = group.Sum(line => (long)line.Quantity)
            })
            .ToList();

        if (aggregated.Any(line => line.TotalQuantity > int.MaxValue))
        {
            return Result<ProcurementBatch>.Failure(Error.Validation(
                "INVALID_PROCUREMENT_BATCH",
                "An aggregated product quantity exceeds the supported limit."));
        }

        // FR-ORD-005 also names delivery zone, but no restaurant-to-zone FK exists yet.
        // SCRUM-271 intentionally groups by market only until Logistics exposes that resolver.
        var batch = new ProcurementBatch
        {
            BatchDate = batchDate,
            MarketId = marketId,
            HubId = hubId,
            Status = ProcurementBatchStatus.Built
        };

        batch._items.AddRange(aggregated.Select(line =>
            new ProcurementBatchItem(
                batch.Id,
                line.MarketProductId,
                line.ProductName,
                (int)line.TotalQuantity)));

        batch._orders.AddRange(input
            .Select(line => line.OrderId)
            .Distinct()
            .Select(orderId => new ProcurementBatchOrder(batch.Id, orderId)));

        batch.TotalItemCount = batch._items.Count;
        var coveredOrderIds = batch._orders.Select(order => order.OrderId).ToList().AsReadOnly();

        batch.RaiseDomainEvent(new ProcurementBatchBuiltDomainEvent(
            batch.Id,
            batch.MarketId,
            batch.BatchDate,
            coveredOrderIds));

        return Result<ProcurementBatch>.Success(batch);
    }

    // ponytail: internal overload is only for legacy test fixtures during the nullable-column rollout.
    internal static Result<ProcurementBatch> Build(
        DateOnly batchDate,
        Guid marketId,
        IEnumerable<(Guid MarketProductId, string ProductName, int Quantity, Guid OrderId)> lines)
    {
        var result = Build(batchDate, marketId, lines, Guid.NewGuid());
        if (result.IsSuccess)
            result.Value.HubId = null;
        return result;
    }

    public Result MergeIn(
        IEnumerable<(Guid MarketProductId, string ProductName, int Quantity, Guid OrderId)> lines,
        IReadOnlyDictionary<Guid, decimal> referencePricesForNewItems,
        DateTime capturedAtUtc)
    {
        if (Status is not ProcurementBatchStatus.Built and not ProcurementBatchStatus.Manifested)
        {
            return Result.Failure(Error.Conflict(
                "BATCH_NOT_MERGEABLE",
                $"Procurement batch '{Id}' cannot accept new orders from status '{Status}'."));
        }

        var input = lines?.ToList() ?? [];
        if (input.Count == 0 || input.Any(line =>
                line.MarketProductId == Guid.Empty ||
                line.OrderId == Guid.Empty ||
                string.IsNullOrWhiteSpace(line.ProductName) ||
                line.Quantity <= 0))
        {
            return Result.Failure(Error.Validation(
                "INVALID_PROCUREMENT_BATCH",
                "A procurement batch requires positive product lines."));
        }

        var existingOrderIds = _orders.Select(order => order.OrderId).ToHashSet();
        var newlyAddedOrderIds = input
            .Select(line => line.OrderId)
            .Distinct()
            .Where(orderId => !existingOrderIds.Contains(orderId))
            .ToList();
        var newlyAddedOrderIdSet = newlyAddedOrderIds.ToHashSet();
        var aggregated = input
            .Where(line => newlyAddedOrderIdSet.Contains(line.OrderId))
            .GroupBy(line => line.MarketProductId)
            .Select(group => new
            {
                MarketProductId = group.Key,
                ProductName = group.First().ProductName.Trim(),
                TotalQuantity = group.Sum(line => (long)line.Quantity)
            })
            .ToList();
        var existingByProduct = _items.ToDictionary(item => item.MarketProductId);

        if (aggregated.Any(line =>
                line.TotalQuantity > int.MaxValue ||
                existingByProduct.TryGetValue(line.MarketProductId, out var item) &&
                item.TotalQuantity + line.TotalQuantity > int.MaxValue))
        {
            return Result.Failure(Error.Validation(
                "INVALID_PROCUREMENT_BATCH",
                "An aggregated product quantity exceeds the supported limit."));
        }

        var missingPrice = Status == ProcurementBatchStatus.Manifested
            ? aggregated.FirstOrDefault(line =>
                !existingByProduct.ContainsKey(line.MarketProductId) &&
                (referencePricesForNewItems is null ||
                 !referencePricesForNewItems.ContainsKey(line.MarketProductId)))
            : null;
        if (missingPrice is not null)
        {
            return Result.Failure(Error.Validation(
                "REFERENCE_PRICE_MISSING",
                $"Reference price is missing for market product '{missingPrice.MarketProductId}'."));
        }

        foreach (var line in aggregated)
        {
            if (existingByProduct.TryGetValue(line.MarketProductId, out var item))
            {
                item.AddQuantity((int)line.TotalQuantity);
                continue;
            }

            var newItem = new ProcurementBatchItem(
                Id,
                line.MarketProductId,
                line.ProductName,
                (int)line.TotalQuantity);
            if (Status == ProcurementBatchStatus.Manifested)
                newItem.SetReferencePrice(referencePricesForNewItems[line.MarketProductId]);
            _items.Add(newItem);
        }

        _orders.AddRange(newlyAddedOrderIds.Select(orderId =>
            new ProcurementBatchOrder(Id, orderId)));

        TotalItemCount = _items.Count;
        UpdatedAt = capturedAtUtc;
        if (newlyAddedOrderIds.Count > 0)
        {
            RaiseDomainEvent(new ProcurementBatchBuiltDomainEvent(
                Id,
                MarketId,
                BatchDate,
                newlyAddedOrderIds.AsReadOnly()));
        }

        return Result.Success();
    }

    public Result Manifest(
        IReadOnlyDictionary<Guid, decimal> referenceUnitPrices,
        DateTime capturedAtUtc)
    {
        if (Status is not ProcurementBatchStatus.Built and not ProcurementBatchStatus.Manifested)
        {
            return Result.Failure(Error.Conflict(
                "BATCH_NOT_MANIFESTABLE",
                $"Procurement batch '{Id}' cannot be manifested from status '{Status}'."));
        }

        var missingItem = _items.FirstOrDefault(item =>
            referenceUnitPrices is null ||
            !referenceUnitPrices.ContainsKey(item.MarketProductId));
        if (missingItem is not null)
        {
            return Result.Failure(Error.Validation(
                "REFERENCE_PRICE_MISSING",
                $"Reference price is missing for market product '{missingItem.MarketProductId}'."));
        }

        foreach (var item in _items)
            item.SetReferencePrice(referenceUnitPrices[item.MarketProductId]);

        Status = ProcurementBatchStatus.Manifested;
        ManifestedAt = capturedAtUtc;
        UpdatedAt = capturedAtUtc;
        RaiseDomainEvent(new ProcurementManifestGeneratedDomainEvent(
            Id,
            MarketId,
            BatchDate,
            capturedAtUtc));

        return Result.Success();
    }

    public Result AssignAgent(Guid agentUserId, DateTime assignedAtUtc)
    {
        if (agentUserId == Guid.Empty)
        {
            return Result.Failure(Error.Validation(
                "INVALID_AGENT",
                "A market agent user ID is required."));
        }

        if (Status == ProcurementBatchStatus.Built)
        {
            return Result.Failure(Error.Conflict(
                "BATCH_NOT_MANIFESTED",
                $"Procurement batch '{Id}' must be manifested before agent assignment."));
        }

        if (Status is ProcurementBatchStatus.Purchasing or ProcurementBatchStatus.HandedOff)
        {
            return Result.Failure(Error.Conflict(
                "BATCH_ALREADY_IN_PROGRESS",
                $"Procurement batch '{Id}' is already in progress."));
        }

        if (Status == ProcurementBatchStatus.Cancelled)
        {
            return Result.Failure(Error.Conflict(
                "BATCH_CANCELLED",
                $"Procurement batch '{Id}' has been cancelled."));
        }

        AssignedAgentUserId = agentUserId;
        AssignedAt = assignedAtUtc;
        UpdatedAt = assignedAtUtc;
        RaiseDomainEvent(new ProcurementAgentAssignedDomainEvent(
            Id,
            MarketId,
            agentUserId,
            assignedAtUtc));

        return Result.Success();
    }

    public Result ConfirmPurchase(
        IReadOnlyDictionary<Guid, (int ActualQuantity, decimal ActualUnitPrice)> lines,
        DateTime capturedAtUtc)
    {
        if (Status == ProcurementBatchStatus.Built)
        {
            return Result.Failure(Error.Conflict(
                "BATCH_NOT_MANIFESTED",
                $"Procurement batch '{Id}' must be manifested before purchase confirmation."));
        }

        if (Status == ProcurementBatchStatus.HandedOff)
        {
            return Result.Failure(Error.Conflict(
                "BATCH_ALREADY_HANDED_OFF",
                $"Procurement batch '{Id}' has already been handed off."));
        }

        if (Status == ProcurementBatchStatus.Cancelled)
        {
            return Result.Failure(Error.Conflict(
                "BATCH_CANCELLED",
                $"Procurement batch '{Id}' has been cancelled."));
        }

        var exemptProductIds = _exceptions
            .Where(exception =>
                !exception.IsDeleted &&
                exception.Type == ProcurementExceptionType.Unavailable)
            .Select(exception => exception.MarketProductId)
            .ToHashSet();
        var requiredItems = _items
            .Where(item => !exemptProductIds.Contains(item.MarketProductId))
            .ToList();

        if (lines is null ||
            lines.Count != requiredItems.Count ||
            requiredItems.Any(item => !lines.ContainsKey(item.MarketProductId)))
        {
            return Result.Failure(Error.Validation(
                "PURCHASE_LINES_MISMATCH",
                "Purchase confirmation must contain exactly one line for every non-exempt batch item."));
        }

        if (lines.Values.Any(line => line.ActualQuantity <= 0 || line.ActualUnitPrice <= 0))
        {
            return Result.Failure(Error.Validation(
                "INVALID_PURCHASE_LINE",
                "Actual quantity and unit price must be greater than zero."));
        }

        foreach (var item in _items)
        {
            if (exemptProductIds.Contains(item.MarketProductId))
            {
                item.ClearPurchase();
                continue;
            }

            var line = lines[item.MarketProductId];
            item.ConfirmPurchase(line.ActualQuantity, line.ActualUnitPrice, capturedAtUtc);
        }

        Status = ProcurementBatchStatus.Purchasing;
        UpdatedAt = capturedAtUtc;
        RaiseDomainEvent(new ProcurementPurchaseConfirmedDomainEvent(
            Id,
            MarketId,
            capturedAtUtc));

        return Result.Success();
    }

    public Result ReportException(
        Guid marketProductId,
        ProcurementExceptionType type,
        int reportedQuantity,
        string? note,
        string? proofImageUrl,
        Guid reportedByUserId,
        DateTime reportedAtUtc)
    {
        if (Status is not ProcurementBatchStatus.Manifested and not ProcurementBatchStatus.Purchasing)
        {
            return Result.Failure(Error.Conflict(
                "BATCH_NOT_REPORTABLE",
                $"Procurement batch '{Id}' cannot accept exceptions from status '{Status}'."));
        }

        if (_items.All(item => item.MarketProductId != marketProductId))
        {
            return Result.Failure(Error.Validation(
                "PRODUCT_NOT_IN_BATCH",
                $"Market product '{marketProductId}' is not part of procurement batch '{Id}'."));
        }

        if (reportedQuantity < 0)
        {
            return Result.Failure(Error.Validation(
                "INVALID_EXCEPTION_QUANTITY",
                "Reported quantity cannot be negative."));
        }

        var exception = new ProcurementException(
            Id,
            marketProductId,
            type,
            reportedQuantity,
            note,
            proofImageUrl,
            reportedByUserId,
            reportedAtUtc);
        _exceptions.Add(exception);
        UpdatedAt = reportedAtUtc;
        RaiseDomainEvent(new ProcurementExceptionReportedDomainEvent(
            Id,
            exception.Id,
            marketProductId,
            type));

        return Result.Success();
    }

    /// <summary>
    /// Cancels the whole session and every order it covers. Only allowed before the agent has
    /// bought anything — once money is spent the shortfall goes through <see cref="ReportException"/>.
    /// </summary>
    public Result Cancel(string? reason, DateTime capturedAtUtc)
    {
        if (Status is not ProcurementBatchStatus.Built and not ProcurementBatchStatus.Manifested)
        {
            return Result.Failure(Error.Conflict(
                "BATCH_NOT_CANCELLABLE",
                $"Procurement batch '{Id}' cannot be cancelled from status '{Status}'."));
        }

        Status = ProcurementBatchStatus.Cancelled;
        CancelledAt = capturedAtUtc;
        CancellationReason = reason;
        UpdatedAt = capturedAtUtc;
        var coveredOrderIds = _orders
            .Select(order => order.OrderId)
            .Distinct()
            .ToList()
            .AsReadOnly();
        RaiseDomainEvent(new ProcurementBatchCancelledDomainEvent(
            Id,
            MarketId,
            reason,
            capturedAtUtc,
            coveredOrderIds));

        return Result.Success();
    }

    public Result HandoverToHub(DateTime capturedAtUtc)
    {
        if (Status == ProcurementBatchStatus.HandedOff)
        {
            return Result.Failure(Error.Conflict(
                "BATCH_ALREADY_HANDED_OFF",
                $"Procurement batch '{Id}' has already been handed off."));
        }

        if (Status != ProcurementBatchStatus.Purchasing)
        {
            return Result.Failure(Error.Conflict(
                "BATCH_NOT_PURCHASED",
                $"Procurement batch '{Id}' must be purchased before handover."));
        }

        if (HubId is null)
        {
            return Result.Failure(Error.Validation(
                "HUB_NOT_CONFIGURED_FOR_MARKET",
                $"Procurement batch '{Id}' has no resolved hub."));
        }

        Status = ProcurementBatchStatus.HandedOff;
        HandedOffAt = capturedAtUtc;
        UpdatedAt = capturedAtUtc;
        var coveredOrderIds = _orders
            .Select(order => order.OrderId)
            .Distinct()
            .ToList()
            .AsReadOnly();
        RaiseDomainEvent(new ProcurementBatchHandedOffDomainEvent(
            Id,
            MarketId,
            HubId,
            capturedAtUtc,
            coveredOrderIds,
            AssignedAgentUserId,
            _items
                .Select(item => new ProcurementPurchasedLine(
                    item.MarketProductId,
                    item.ActualQuantity ?? 0,
                    item.ActualUnitPrice))
                .ToList()
                .AsReadOnly()));

        return Result.Success();
    }
}
