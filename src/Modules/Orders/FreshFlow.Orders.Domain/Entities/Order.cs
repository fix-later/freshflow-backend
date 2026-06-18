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
        OrderGroupId = orderGroupId;
        ScheduledOrderId = scheduledOrderId;

        RaiseDomainEvent(new OrderCreatedDomainEvent(Id, RestaurantId, DateTime.UtcNow));
    }

    public Guid RestaurantId { get; private set; }
    public Guid? OrderGroupId { get; private set; }
    public Guid? ScheduledOrderId { get; private set; }
    public OrderStatus Status { get; private set; }
    public OrderPaymentStatus PaymentStatus { get; private set; }
    public DateTime? ScheduledFor { get; private set; }
    public decimal TotalAmount { get; private set; }
    public string? Notes { get; private set; }
    public DateTime? CancelledAt { get; private set; }
    public string? CancellationReason { get; private set; }

    public IReadOnlyCollection<OrderItem> Items => _items.AsReadOnly();

    /// <summary>
    /// Adds a line item while the order is still a draft (cart). Recalculates <see cref="TotalAmount"/>.
    /// </summary>
    public Result AddItem(Guid marketProductId, string productNameSnapshot, int quantity, decimal unitPrice)
    {
        if (Status != OrderStatus.Draft)
            return Result.Failure(Error.Conflict(
                "ORDER_NOT_DRAFT", "Items can only be added while the order is in draft status."));

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
        RecalculateTotal();

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

    /// <summary>
    /// Transitions the order from Draft to Confirmed, locking item prices and accruing
    /// the total as outstanding debt (B2B credit model).
    /// </summary>
    public Result Confirm()
    {
        var canConfirm = CanConfirm();
        if (canConfirm.IsFailure)
            return canConfirm;

        foreach (var item in _items)
            item.LockPrice(item.UnitPrice);

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

    /// <summary>
    /// Advances the order through the post-confirm logistics pipeline
    /// (Batched → PickedUp → AtHub → Delivering → Delivered).
    /// </summary>
    public Result AdvanceStatus(OrderStatus next)
    {
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

    private void RecalculateTotal() => TotalAmount = _items.Sum(i => i.Subtotal);
}
