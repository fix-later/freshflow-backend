using FreshFlow.Orders.Domain.Enums;
using FreshFlow.Orders.Domain.Events;
using FreshFlow.SharedKernel.Application;
using FreshFlow.SharedKernel.Domain;

namespace FreshFlow.Orders.Domain.Entities;

public sealed class Order : AggregateRoot
{
    // Valid forward transitions for the logistics pipeline (post-confirm).
    private static readonly Dictionary<OrderStatus, OrderStatus[]> AllowedTransitions = new()
    {
        [OrderStatus.Draft] = [OrderStatus.Confirmed, OrderStatus.Cancelled],
        [OrderStatus.Confirmed] = [OrderStatus.Batched, OrderStatus.Cancelled],
        [OrderStatus.Batched] = [OrderStatus.PickedUp],
        [OrderStatus.PickedUp] = [OrderStatus.AtHub],
        [OrderStatus.AtHub] = [OrderStatus.Delivering],
        [OrderStatus.Delivering] = [OrderStatus.Delivered],
        [OrderStatus.Delivered] = [],
        [OrderStatus.Cancelled] = []
    };

    private readonly List<OrderItem> _items = [];

    private Order() { } // EF Core

    public Order(Guid restaurantId, DateTime? scheduledFor, string? notes,
        Guid? orderGroupId = null, Guid? scheduledOrderId = null)
    {
        RestaurantId = restaurantId;
        Status = OrderStatus.Draft;
        PaymentStatus = OrderPaymentStatus.NotApplicable;
        ScheduledFor = scheduledFor;
        Notes = notes;
        TotalAmount = 0;
        SubtotalAmount = 0;
        OrderGroupId = orderGroupId;
        ScheduledOrderId = scheduledOrderId;

        RaiseDomainEvent(new OrderCreatedDomainEvent(Id, RestaurantId, DateTime.UtcNow));
    }

    public Guid RestaurantId { get; private set; }
    public Guid? MarketId { get; private set; }
    public Guid? OrderGroupId { get; private set; }
    public Guid? ScheduledOrderId { get; private set; }
    public OrderStatus Status { get; private set; }
    public OrderPaymentStatus PaymentStatus { get; private set; }
    public DateTime? ScheduledFor { get; private set; }
    public decimal TotalAmount { get; private set; }
    public decimal SubtotalAmount { get; private set; }
    public decimal VatAmount { get; private set; }
    public decimal DeliveryFee { get; private set; }
    public decimal DeliveryDistanceKm { get; private set; }
    public int? DeliveryDistanceMeters { get; private set; }
    public int? DeliveryDurationSeconds { get; private set; }
    public DateTime? DeliveryFeeCalculatedAt { get; private set; }
    public string? RoutingProvider { get; private set; }
    public decimal? DeliveryOriginLatitude { get; private set; }
    public decimal? DeliveryOriginLongitude { get; private set; }
    public string? Notes { get; private set; }
    public Guid? DeliveryAddressId { get; private set; }
    public string? DeliveryRecipientName { get; private set; }
    public string? DeliveryPhone { get; private set; }
    public string? DeliveryAddressLine { get; private set; }
    public decimal? DeliveryLatitude { get; private set; }
    public decimal? DeliveryLongitude { get; private set; }
    public DateTime? CancelledAt { get; private set; }
    public string? CancellationReason { get; private set; }
    public DateTime? ConfirmedReceiptAt { get; private set; }

    public IReadOnlyCollection<OrderItem> Items => _items.AsReadOnly();

    public Result CaptureDeliveryAddress(
        Guid addressId,
        string? recipientName,
        string? phone,
        string addressLine,
        decimal? latitude,
        decimal? longitude)
    {
        if (Status != OrderStatus.Draft || DeliveryAddressId.HasValue)
            return Result.Failure(Error.Conflict(
                "DELIVERY_ADDRESS_ALREADY_CAPTURED",
                "The delivery address can only be captured once while the order is a draft."));

        DeliveryAddressId = addressId;
        DeliveryRecipientName = recipientName;
        DeliveryPhone = phone;
        DeliveryAddressLine = addressLine;
        DeliveryLatitude = latitude;
        DeliveryLongitude = longitude;

        return Result.Success();
    }

    /// <summary>
    /// Adds a line item while the order is still a draft (cart). Recalculates <see cref="TotalAmount"/>.
    /// </summary>
    public Result AddItem(
        Guid marketProductId,
        string productNameSnapshot,
        int quantity,
        decimal unitPrice,
        Guid? marketId = null)
    {
        if (Status != OrderStatus.Draft)
            return Result.Failure(Error.Conflict(
                "ORDER_NOT_DRAFT", "Items can only be added while the order is in draft status."));

        if (marketId.HasValue && MarketId.HasValue && marketId != MarketId)
            return Result.Failure(Error.Validation(
                "ORDER_MARKET_MISMATCH", "An order can only contain products from one market."));

        if (marketId.HasValue)
            MarketId = marketId;
        var item = new OrderItem(marketProductId, productNameSnapshot, quantity, unitPrice);
        _items.Add(item);
        RecalculateTotal();

        return Result.Success();
    }

    /// <summary>
    /// Updates a line item while the order is still a draft (cart).
    /// </summary>
    public Result UpdateItem(Guid itemId, int quantity)
    {
        if (Status != OrderStatus.Draft)
            return Result.Failure(Error.Conflict(
                "ORDER_NOT_DRAFT", "Items can only be updated while the order is in draft status."));

        var item = _items.FirstOrDefault(i => i.Id == itemId);
        if (item is null)
            return Result.Failure(Error.NotFound("ORDER_ITEM", itemId));

        item.UpdateQuantity(quantity);
        RecalculateTotal();

        return Result.Success();
    }

    /// <summary>
    /// Removes a line item while the order is still a draft (cart).
    /// </summary>
    public Result RemoveItem(Guid itemId)
    {
        if (Status != OrderStatus.Draft)
            return Result.Failure(Error.Conflict(
                "ORDER_NOT_DRAFT", "Items can only be removed while the order is in draft status."));

        var item = _items.FirstOrDefault(i => i.Id == itemId);
        if (item is null)
            return Result.Failure(Error.NotFound("ORDER_ITEM", itemId));

        _items.Remove(item);
        if (_items.Count == 0)
            MarketId = null;
        RecalculateTotal();

        return Result.Success();
    }

    public Result AssignMarket(Guid marketId)
    {
        if (marketId == Guid.Empty)
            return Result.Failure(Error.Validation("INVALID_MARKET", "A market is required."));
        if (MarketId.HasValue && MarketId != marketId)
            return Result.Failure(Error.Validation(
                "ORDER_MARKET_MISMATCH", "An order can only contain products from one market."));
        MarketId = marketId;
        return Result.Success();
    }

    /// <summary>
    /// Transitions the order from Draft to Confirmed, locking item prices and accruing
    /// the total as outstanding debt (B2B credit model).
    /// </summary>
    public Result CanConfirm()
    {
        if (Status != OrderStatus.Draft)
            return Result.Failure(Error.Conflict(
                "ORDER_NOT_DRAFT", "Only a draft order can be confirmed."));

        if (_items.Count == 0)
            return Result.Failure(Error.Validation(
                "ORDER_EMPTY", "Cannot confirm an order with no items."));

        return Result.Success();
    }

    public Result ApplyConfirmationPricing(
        IReadOnlyDictionary<Guid, OrderItemTaxSnapshot> taxesByMarketProduct,
        decimal deliveryDistanceKm,
        decimal deliveryFee,
        int? deliveryDistanceMeters = null,
        int? deliveryDurationSeconds = null,
        DateTime? deliveryFeeCalculatedAt = null,
        string? routingProvider = null,
        decimal? deliveryOriginLatitude = null,
        decimal? deliveryOriginLongitude = null)
    {
        if (Status != OrderStatus.Draft)
            return Result.Failure(Error.Conflict(
                "ORDER_NOT_DRAFT", "Pricing can only be applied while the order is a draft."));
        if (deliveryDistanceKm < 0m || deliveryFee < 0m)
            return Result.Failure(Error.Validation(
                "INVALID_DELIVERY_FEE", "Delivery distance and fee must be non-negative."));
        if (deliveryDistanceMeters is < 0 || deliveryDurationSeconds is < 0)
            return Result.Failure(Error.Validation(
                "INVALID_DELIVERY_DISTANCE", "Delivery distance and duration must be non-negative."));
        var hasSnapshot = deliveryDistanceMeters.HasValue
            || deliveryDurationSeconds.HasValue
            || deliveryFeeCalculatedAt.HasValue
            || routingProvider is not null
            || deliveryOriginLatitude.HasValue
            || deliveryOriginLongitude.HasValue;
        var hasCompleteSnapshot = deliveryDistanceMeters.HasValue
            && deliveryDurationSeconds.HasValue
            && deliveryFeeCalculatedAt.HasValue
            && !string.IsNullOrWhiteSpace(routingProvider)
            && deliveryOriginLatitude is >= -90m and <= 90m
            && deliveryOriginLongitude is >= -180m and <= 180m;
        if (hasSnapshot && !hasCompleteSnapshot)
            return Result.Failure(Error.Validation(
                "INVALID_DELIVERY_SNAPSHOT", "A complete delivery road-distance snapshot is required."));

        foreach (var item in _items)
        {
            if (!taxesByMarketProduct.TryGetValue(item.MarketProductId, out var tax))
                return Result.Failure(Error.Validation(
                    "VAT_RATE_MISSING", $"VAT rate is missing for market product '{item.MarketProductId}'."));

            item.LockPricing(item.UnitPrice, tax.Code, tax.Percent);
        }

        SubtotalAmount = _items.Sum(item => item.LockedTotal ?? 0m);
        VatAmount = _items.Sum(item => item.LockedVatAmount ?? 0m);
        DeliveryDistanceKm = deliveryDistanceKm;
        DeliveryFee = deliveryFee;
        DeliveryDistanceMeters = deliveryDistanceMeters;
        DeliveryDurationSeconds = deliveryDurationSeconds;
        DeliveryFeeCalculatedAt = deliveryFeeCalculatedAt;
        RoutingProvider = routingProvider;
        DeliveryOriginLatitude = deliveryOriginLatitude;
        DeliveryOriginLongitude = deliveryOriginLongitude;
        TotalAmount = SubtotalAmount + VatAmount + DeliveryFee;
        return Result.Success();
    }

    /// <summary>
    /// Transitions the order from Draft to Confirmed, locking item prices and accruing
    /// the total as outstanding debt (B2B credit model).
    /// </summary>
    public Result Confirm()
    {
        var canConfirm = CanConfirm();
        if (canConfirm.IsFailure)
            return canConfirm;

        if (_items.Any(item => item.LockedUnitPrice is null))
        {
            var defaultTaxes = _items
                .Select(item => item.MarketProductId)
                .Distinct()
                .ToDictionary(id => id, _ => new OrderItemTaxSnapshot("KCT", 0m));
            var pricing = ApplyConfirmationPricing(defaultTaxes, 0m, 0m);
            if (pricing.IsFailure)
                return pricing;
        }

        TransitionTo(OrderStatus.Confirmed);
        PaymentStatus = OrderPaymentStatus.Outstanding;

        RaiseDomainEvent(new OrderConfirmedDomainEvent(Id, RestaurantId, TotalAmount, DateTime.UtcNow));

        return Result.Success();
    }

    /// <summary>
    /// Pushes <see cref="ScheduledFor"/> to a later delivery cycle, e.g. when confirmation
    /// happens after the daily cutoff time.
    /// </summary>
    public Result RescheduleFor(DateTime newScheduledFor)
    {
        if (Status is OrderStatus.Cancelled or OrderStatus.Delivered)
            return Result.Failure(Error.Conflict(
                "ORDER_CANNOT_RESCHEDULE", $"An order in status '{Status}' cannot be rescheduled."));

        ScheduledFor = newScheduledFor;

        return Result.Success();
    }

    /// <summary>
    /// Cancels the order. Any accrued debt is waived since the order never completed.
    /// </summary>
    public Result Cancel(string? reason)
    {
        if (!AllowedTransitions.TryGetValue(Status, out var next) || !next.Contains(OrderStatus.Cancelled))
            return Result.Failure(Error.Conflict(
                "ORDER_NOT_CANCELLABLE", $"An order in status '{Status}' cannot be cancelled."));

        return CancelInternal(reason);
    }

    /// <summary>
    /// Cancels the order because the whole market session it belongs to was cancelled. Allowed from
    /// Batched, unlike <see cref="Cancel"/>: a session only cancels before its agent buys anything,
    /// so a batched order is still safe to drop. Nobody may cancel a batched order on its own.
    /// </summary>
    public Result CancelWithSession(string? reason)
    {
        if (Status is not OrderStatus.Confirmed and not OrderStatus.Batched)
            return Result.Failure(Error.Conflict(
                "ORDER_NOT_CANCELLABLE", $"An order in status '{Status}' cannot be cancelled."));

        return CancelInternal(reason);
    }

    private Result CancelInternal(string? reason)
    {
        TransitionTo(OrderStatus.Cancelled);
        CancelledAt = DateTime.UtcNow;
        CancellationReason = reason;

        if (PaymentStatus == OrderPaymentStatus.Outstanding)
            PaymentStatus = OrderPaymentStatus.Waived;

        RaiseDomainEvent(new OrderCancelledDomainEvent(Id, RestaurantId, reason, DateTime.UtcNow));

        return Result.Success();
    }

    /// <summary>
    /// Records the fulfilled quantity for an order item when operations flags a shortage or damage.
    /// </summary>
    public Result RecordActualQuantity(Guid itemId, decimal actualQuantity)
    {
        if (Status is OrderStatus.Draft or OrderStatus.Cancelled)
            return Result.Failure(Error.Conflict(
                "ORDER_CANNOT_ADJUST", $"An order in status '{Status}' cannot be adjusted."));

        var item = _items.FirstOrDefault(i => i.Id == itemId);
        if (item is null)
            return Result.Failure(Error.NotFound("ORDER_ITEM", itemId));

        if (actualQuantity < 0m || actualQuantity > item.Quantity)
            return Result.Failure(Error.Validation(
                "INVALID_ACTUAL_QUANTITY",
                "Actual quantity must be non-negative and cannot exceed ordered quantity."));

        item.RecordActualQuantity(actualQuantity);

        return Result.Success();
    }

    public Result ApplyProcurementActuals(
        IReadOnlyDictionary<Guid, OrderItemProcurementActual> actualsByItem)
    {
        if (Status != OrderStatus.Batched)
            return Result.Failure(Error.Conflict(
                "ORDER_NOT_BATCHED",
                $"Procurement actuals cannot be applied to an order in status '{Status}'."));

        foreach (var (itemId, actual) in actualsByItem)
        {
            var item = _items.FirstOrDefault(candidate => candidate.Id == itemId);
            if (item is null)
                return Result.Failure(Error.NotFound("ORDER_ITEM", itemId));
            if (actual.Quantity < 0m || actual.Quantity > item.Quantity)
                return Result.Failure(Error.Validation(
                    "INVALID_ACTUAL_QUANTITY",
                    "Actual quantity must be non-negative and cannot exceed ordered quantity."));
            if (actual.Quantity > 0m && actual.UnitPrice is null or <= 0m)
                return Result.Failure(Error.Validation(
                    "INVALID_ACTUAL_UNIT_PRICE",
                    "A positive actual quantity requires a positive actual unit price."));
        }

        foreach (var (itemId, actual) in actualsByItem)
        {
            var item = _items.Single(candidate => candidate.Id == itemId);
            item.RecordProcurementActuals(actual.Quantity, actual.UnitPrice);
        }

        return Result.Success();
    }

    /// <summary>
    /// Confirms that the restaurant has received an already-delivered order.
    /// </summary>
    public Result ConfirmReceipt(DateTime? confirmedAtUtc = null)
    {
        if (Status != OrderStatus.Delivered)
            return Result.Failure(Error.Conflict(
                "ORDER_NOT_DELIVERED", "Receipt can only be confirmed after the order is delivered."));

        if (ConfirmedReceiptAt.HasValue)
            return Result.Failure(Error.Conflict(
                "ORDER_RECEIPT_ALREADY_CONFIRMED", "Receipt has already been confirmed for this order."));

        ConfirmedReceiptAt = confirmedAtUtc ?? DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;

        return Result.Success();
    }

    /// <summary>
    /// Advances the order through the post-confirm logistics pipeline
    /// (Batched → PickedUp → AtHub → Delivering → Delivered).
    /// </summary>
    public Result AdvanceStatus(OrderStatus next)
    {
        // Cancelling must go through Cancel() so the reason, timestamp and debt waiver are recorded.
        if (next == OrderStatus.Cancelled)
            return Result.Failure(Error.Conflict(
                "ORDER_INVALID_TRANSITION", "Use Cancel to cancel an order."));

        if (!AllowedTransitions.TryGetValue(Status, out var allowed) || !allowed.Contains(next))
            return Result.Failure(Error.Conflict(
                "ORDER_INVALID_TRANSITION", $"Cannot transition order from '{Status}' to '{next}'."));

        TransitionTo(next);
        return Result.Success();
    }

    private void TransitionTo(OrderStatus next)
    {
        var previous = Status;
        Status = next;

        RaiseDomainEvent(new OrderStatusChangedDomainEvent(Id, RestaurantId, previous, next, DateTime.UtcNow));
    }

    private void RecalculateTotal()
    {
        SubtotalAmount = _items.Sum(i => i.Subtotal);
        VatAmount = 0m;
        DeliveryFee = 0m;
        DeliveryDistanceKm = 0m;
        DeliveryDistanceMeters = null;
        DeliveryDurationSeconds = null;
        DeliveryFeeCalculatedAt = null;
        RoutingProvider = null;
        DeliveryOriginLatitude = null;
        DeliveryOriginLongitude = null;
        TotalAmount = SubtotalAmount;
    }
}

public sealed record OrderItemTaxSnapshot(string Code, decimal Percent);

public sealed record OrderItemProcurementActual(decimal Quantity, decimal? UnitPrice);
