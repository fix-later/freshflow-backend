using FreshFlow.Procurement.Domain.Enums;
using FreshFlow.Procurement.Domain.Events;
using FreshFlow.SharedKernel.Application;
using FreshFlow.SharedKernel.Domain;

namespace FreshFlow.Procurement.Domain.Entities;

public sealed class ProcurementBatch : AggregateRoot
{
    private readonly List<ProcurementBatchItem> _items = [];
    private readonly List<ProcurementBatchOrder> _orders = [];

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

    public IReadOnlyCollection<ProcurementBatchItem> Items => _items.AsReadOnly();
    public IReadOnlyCollection<ProcurementBatchOrder> Orders => _orders.AsReadOnly();

    public static Result<ProcurementBatch> Build(
        DateOnly batchDate,
        Guid marketId,
        IEnumerable<(Guid MarketProductId, string ProductName, int Quantity, Guid OrderId)> lines)
    {
        var input = lines?.ToList() ?? [];

        if (batchDate == default || marketId == Guid.Empty || input.Count == 0 ||
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

        if (lines is null ||
            lines.Count != _items.Count ||
            _items.Any(item => !lines.ContainsKey(item.MarketProductId)))
        {
            return Result.Failure(Error.Validation(
                "PURCHASE_LINES_MISMATCH",
                "Purchase confirmation must contain exactly one line for every batch item."));
        }

        if (lines.Values.Any(line => line.ActualQuantity <= 0 || line.ActualUnitPrice <= 0))
        {
            return Result.Failure(Error.Validation(
                "INVALID_PURCHASE_LINE",
                "Actual quantity and unit price must be greater than zero."));
        }

        foreach (var item in _items)
        {
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

    public Result HandoverToHub(Guid? hubId, DateTime capturedAtUtc)
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

        Status = ProcurementBatchStatus.HandedOff;
        HandedOffAt = capturedAtUtc;
        HubId = hubId;
        UpdatedAt = capturedAtUtc;
        var coveredOrderIds = _orders
            .Select(order => order.OrderId)
            .Distinct()
            .ToList()
            .AsReadOnly();
        RaiseDomainEvent(new ProcurementBatchHandedOffDomainEvent(
            Id,
            MarketId,
            hubId,
            capturedAtUtc,
            coveredOrderIds));

        return Result.Success();
    }
}
