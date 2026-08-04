using FreshFlow.Contracts;
using FreshFlow.Orders.Application.Abstractions;
using FreshFlow.Orders.Domain.Entities;
using FreshFlow.Orders.Domain.Enums;
using FreshFlow.SharedKernel.Application;
using MediatR;
using Microsoft.Extensions.Logging;

namespace FreshFlow.Orders.Application.EventHandlers;

public sealed class ProcurementBatchHandedOffIntegrationEventHandler(
    IOrderRepository orders,
    IPublisher publisher,
    ILogger<ProcurementBatchHandedOffIntegrationEventHandler> logger)
    : INotificationHandler<ProcurementBatchHandedOffIntegrationEvent>,
      IProcurementHandoverOrderFinalizer
{
    public async Task Handle(
        ProcurementBatchHandedOffIntegrationEvent notification,
        CancellationToken cancellationToken)
    {
        var orderEvents = await FinalizeAsync(notification, cancellationToken);
        foreach (var orderEvent in orderEvents)
            await publisher.Publish(orderEvent, cancellationToken);
    }

    public async Task<IReadOnlyList<INotification>> FinalizeAsync(
        ProcurementBatchHandedOffIntegrationEvent notification,
        CancellationToken cancellationToken)
    {
        var orderEvents = new List<INotification>();
        var orderIds = notification.CoveredOrderIds
            .Distinct()
            .Order()
            .ToArray();

        var result = await orders.ExecuteInSerializableTransactionAsync(async ct =>
        {
            var coveredOrders = await orders.FindByIdsAsync(orderIds, ct);
            if (coveredOrders.Count != orderIds.Length)
                return Result.Failure(Error.Conflict(
                    "PROCUREMENT_ORDER_MISSING",
                    "One or more orders covered by the procurement batch no longer exist."));

            var batchedOrders = coveredOrders
                .Where(order => order.Status == OrderStatus.Batched)
                .ToList();
            if (batchedOrders.Count == 0)
            {
                foreach (var order in coveredOrders.Where(order => order.Status == OrderStatus.PickedUp))
                {
                    var atHub = order.AdvanceStatus(OrderStatus.AtHub);
                    if (atHub.IsFailure)
                        return atHub;
                }

                CaptureEvents(coveredOrders, orderEvents);
                return Result.Success();
            }

            if (batchedOrders.Count != coveredOrders.Count)
                return Result.Failure(Error.Conflict(
                    "PROCUREMENT_ORDER_STATE_CONFLICT",
                    "Covered orders must enter the hub together."));

            var demands = batchedOrders
                .SelectMany(order => order.Items.Select(item => new Demand(order.Id, item)))
                .ToList();
            var actualsResult = ResolveActuals(notification.PurchaseActuals, demands);
            if (actualsResult.IsFailure)
                return Result.Failure(actualsResult.Error);

            var actuals = actualsResult.Value;
            var requestedByProduct = demands
                .GroupBy(demand => demand.Item.MarketProductId)
                .ToDictionary(group => group.Key, group => group.Sum(demand => demand.Item.Quantity));
            var consumed = requestedByProduct
                .Select(entry => new StockReservation(
                    entry.Key,
                    Math.Min(entry.Value, actuals[entry.Key].ActualQuantity)))
                .Where(reservation => reservation.Quantity > 0)
                .OrderBy(reservation => reservation.MarketProductId)
                .ToArray();
            var released = requestedByProduct
                .Select(entry => new StockReservation(
                    entry.Key,
                    entry.Value - Math.Min(entry.Value, actuals[entry.Key].ActualQuantity)))
                .Where(reservation => reservation.Quantity > 0)
                .OrderBy(reservation => reservation.MarketProductId)
                .ToArray();

            if (!await orders.ConsumeStockAsync(consumed, ct) ||
                !await orders.ReleaseStockAsync(released, ct))
            {
                return Result.Failure(Error.Conflict(
                    "STOCK_RESERVATION_CONFLICT",
                    "The order stock reservation could not be finalized."));
            }

            var allocations = Allocate(demands, actuals);
            foreach (var order in batchedOrders)
            {
                if (notification.PurchaseActuals is not null)
                {
                    var orderActuals = order.Items
                        .Where(item => allocations.ContainsKey(item.Id))
                        .ToDictionary(item => item.Id, item => allocations[item.Id]);
                    var apply = order.ApplyProcurementActuals(orderActuals);
                    if (apply.IsFailure)
                        return apply;
                }

                var pickup = order.AdvanceStatus(OrderStatus.PickedUp);
                if (pickup.IsFailure)
                    return pickup;
                var atHub = order.AdvanceStatus(OrderStatus.AtHub);
                if (atHub.IsFailure)
                    return atHub;
            }

            CaptureEvents(coveredOrders, orderEvents);
            return Result.Success();
        }, cancellationToken);

        if (result.IsFailure)
        {
            logger.LogWarning(
                "Rejected procurement handover for BatchId={BatchId}: {ErrorCode}.",
                notification.BatchId,
                result.Error.Code);
            throw new ProcurementHandoverRejectedException(
                result.Error.Code,
                result.Error.Message);
        }

        return orderEvents.AsReadOnly();
    }

    private static void CaptureEvents(
        IEnumerable<Order> coveredOrders,
        ICollection<INotification> destination)
    {
        foreach (var order in coveredOrders)
        {
            foreach (var domainEvent in order.DomainEvents)
                destination.Add(domainEvent);
            order.ClearDomainEvents();
        }
    }

    private static Result<IReadOnlyDictionary<Guid, ProcurementPurchaseActual>> ResolveActuals(
        IReadOnlyList<ProcurementPurchaseActual>? purchaseActuals,
        IReadOnlyList<Demand> demands)
    {
        var requestedProductIds = demands
            .Select(demand => demand.Item.MarketProductId)
            .Distinct()
            .ToArray();

        if (purchaseActuals is null)
        {
            return Result<IReadOnlyDictionary<Guid, ProcurementPurchaseActual>>.Success(
                requestedProductIds.ToDictionary(
                    id => id,
                    id => new ProcurementPurchaseActual(
                        id,
                        demands.Where(demand => demand.Item.MarketProductId == id)
                            .Sum(demand => demand.Item.Quantity),
                        null)));
        }

        if (purchaseActuals.Count != requestedProductIds.Length ||
            purchaseActuals.Select(line => line.MarketProductId).Distinct().Count() != purchaseActuals.Count ||
            requestedProductIds.Any(id => purchaseActuals.All(line => line.MarketProductId != id)) ||
            purchaseActuals.Any(line =>
                line.ActualQuantity < 0 ||
                line.ActualQuantity > 0 && line.ActualUnitPrice is null or <= 0m))
        {
            return Result<IReadOnlyDictionary<Guid, ProcurementPurchaseActual>>.Failure(
                Error.Validation(
                    "PURCHASE_ACTUALS_MISMATCH",
                    "Handover purchase actuals must contain one valid line for every ordered product."));
        }

        return Result<IReadOnlyDictionary<Guid, ProcurementPurchaseActual>>.Success(
            purchaseActuals.ToDictionary(line => line.MarketProductId));
    }

    private static IReadOnlyDictionary<Guid, OrderItemProcurementActual> Allocate(
        IReadOnlyList<Demand> demands,
        IReadOnlyDictionary<Guid, ProcurementPurchaseActual> actuals)
    {
        var result = new Dictionary<Guid, OrderItemProcurementActual>();

        foreach (var productGroup in demands.GroupBy(demand => demand.Item.MarketProductId))
        {
            var orderedDemands = productGroup
                .OrderBy(demand => demand.OrderId)
                .ThenBy(demand => demand.Item.Id)
                .ToList();
            var totalRequested = orderedDemands.Sum(demand => demand.Item.Quantity);
            var actual = actuals[productGroup.Key];
            var distributable = Math.Min(totalRequested, actual.ActualQuantity);
            var shares = orderedDemands
                .Select(demand =>
                {
                    var exact = (decimal)distributable * demand.Item.Quantity / totalRequested;
                    var quantity = decimal.Floor(exact * 100m) / 100m;
                    return new Share(demand, quantity, exact - quantity);
                })
                .ToList();
            var remainingHundredths = (int)decimal.Round(
                (distributable - shares.Sum(share => share.Quantity)) * 100m,
                0,
                MidpointRounding.AwayFromZero);

            foreach (var share in shares
                         .OrderByDescending(share => share.Remainder)
                         .ThenBy(share => share.Demand.OrderId)
                         .ThenBy(share => share.Demand.Item.Id)
                         .Take(remainingHundredths))
            {
                share.Quantity += 0.01m;
            }

            foreach (var share in shares)
            {
                result[share.Demand.Item.Id] = new OrderItemProcurementActual(
                    share.Quantity,
                    share.Quantity > 0m ? actual.ActualUnitPrice : null);
            }
        }

        return result;
    }

    private sealed record Demand(Guid OrderId, OrderItem Item);

    private sealed class Share(Demand demand, decimal quantity, decimal remainder)
    {
        public Demand Demand { get; } = demand;
        public decimal Quantity { get; set; } = quantity;
        public decimal Remainder { get; } = remainder;
    }
}
