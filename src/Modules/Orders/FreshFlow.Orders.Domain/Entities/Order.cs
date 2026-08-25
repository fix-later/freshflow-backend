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
    public Guid? MarketSessionId { get; private set; }
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
    public DateTime? ConfirmedAt { get; private set; }

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
        Guid? marketId = null,
        string? packingCodeSnapshot = null,
        decimal? packingWeightKgSnapshot = null)
    {
        if (Status != OrderStatus.Draft)
            return Result.Failure(Error.Conflict(
                "ORDER_NOT_DRAFT", "Items can only be added while the order is in draft status."));

        if (marketId.HasValue && MarketId.HasValue && marketId != MarketId)
            return Result.Failure(Error.Validation(
                "ORDER_MARKET_MISMATCH", "An order can only contain products from one market."));

        if (marketId.HasValue)
            MarketId = marketId;
        var item = new OrderItem(
            marketProductId, productNameSnapshot, quantity, unitPrice,
            packingCodeSnapshot, packingWeightKgSnapshot);
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

    /// <summary>
    /// Updates the order-level notes while the order is still a draft (cart).
    /// </summary>
    public Result UpdateNotes(string? notes)
    {
        if (Status != OrderStatus.Draft)
            return Result.Failure(Error.Conflict(
                "ORDER_NOT_DRAFT", "Notes can only be updated while the order is in draft status."));

        Notes = notes;

        return Result.Success();
    }

    /// <summary>
    /// Updates the desired delivery date while the order is still a draft (cart). Unlike
    /// <see cref="RescheduleFor"/> (which pushes a confirmed order past a missed cutoff), this
    /// is a plain field edit and is only allowed pre-confirmation.
    /// </summary>
    public Result UpdateScheduledFor(DateTime? scheduledFor)
    {
        if (Status != OrderStatus.Draft)
            return Result.Failure(Error.Conflict(
                "ORDER_NOT_DRAFT", "The delivery date can only be updated while the order is in draft status."));

        ScheduledFor = scheduledFor;

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
    public Result Confirm(Guid? marketSessionId = null, DateTime? confirmedAtUtc = null)
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

        if (marketSessionId == Guid.Empty)
            return Result.Failure(Error.Validation(
                "INVALID_MARKET_SESSION", "A market session ID cannot be empty."));
        if (confirmedAtUtc.HasValue && confirmedAtUtc.Value.Kind != DateTimeKind.Utc)
            return Result.Failure(Error.Validation(
                "INVALID_CONFIRMATION_TIME", "The confirmation timestamp must be UTC."));

        var confirmedAt = confirmedAtUtc ?? DateTime.UtcNow;
        MarketSessionId = marketSessionId;
        ConfirmedAt = confirmedAt;
        TransitionTo(OrderStatus.Confirmed);
        PaymentStatus = OrderPaymentStatus.Outstanding;

        RaiseDomainEvent(new OrderConfirmedDomainEvent(Id, RestaurantId, TotalAmount, confirmedAt));

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

    /// <summary>
    /// Cancels the order because the driver marked its delivery as failed. Allowed only from
    /// Delivering. One failure is final — no retry, no re-delivery path back to a routable status.
    /// Deliberately NOT added to <see cref="AllowedTransitions"/>: that dictionary also gates the
    /// restaurant-facing <see cref="Cancel"/>, and a restaurant must never be able to cancel an
    /// order that is already on the truck. Same reasoning as <see cref="CancelWithSession"/>.
    /// </summary>
    public Result CancelForFailedDelivery(string? reason)
    {
        if (Status != OrderStatus.Delivering)
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

        // AUDIT-2026-08-23 C4: baseline off the previous actual (falling back to ordered) so a
        // hub discrepancy or admin correction applied after ApplyProcurementActuals refunds only
        // the newly-lost quantity, not the whole line again.
        var previousQuantity = item.ActualQuantity ?? item.Quantity;
        item.RecordActualQuantity(actualQuantity);
        RaiseShortfallIfReduced(item, previousQuantity, actualQuantity);

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

            // C1/C4: baseline off the previous actual (falling back to ordered) so re-applying
            // procurement actuals can't double-refund a quantity already refunded once.
            var previousQuantity = item.ActualQuantity ?? item.Quantity;
            item.RecordProcurementActuals(actual.Quantity, actual.UnitPrice);

            // The restaurant was charged at confirm time for the ordered quantity; if the agent
            // bought less, refund the shortfall (goods + its proportional VAT) via the credit
            // ledger. TotalAmount/SubtotalAmount/VatAmount stay the immutable confirmation
            // snapshot the VAT invoice is issued from — do not rewrite them here (the invoice
            // itself now reads delivered quantity straight off ActualQuantity, see C4).
            RaiseShortfallIfReduced(item, previousQuantity, actual.Quantity);
        }

        return Result.Success();
    }

    /// <summary>
    /// Raises <see cref="OrderProcurementShortfallDomainEvent"/> when an item's fulfilled
    /// quantity drops below its previous value, refunding goods + proportional VAT for the
    /// delta. Shared by <see cref="ApplyProcurementActuals"/> (procurement handoff) and
    /// <see cref="RecordActualQuantity"/> (hub discrepancy / admin correction) so both paths
    /// measure the shortfall the same way and neither can double-refund the other's delta.
    /// </summary>
    private void RaiseShortfallIfReduced(OrderItem item, decimal previousQuantity, decimal newQuantity)
    {
        if (newQuantity >= previousQuantity || !item.LockedUnitPrice.HasValue)
            return;

        var shortfallQuantity = previousQuantity - newQuantity;
        var goods = shortfallQuantity * item.LockedUnitPrice.Value;
        var vat = decimal.Round(
            goods * (item.VatRatePercent ?? 0m) / 100m, 2, MidpointRounding.AwayFromZero);
        var refundAmount = goods + vat;

        if (refundAmount > 0m)
        {
            RaiseDomainEvent(new OrderProcurementShortfallDomainEvent(
                Id, RestaurantId, item.Id, item.ProductNameSnapshot,
                shortfallQuantity, refundAmount, DateTime.UtcNow));
        }
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
