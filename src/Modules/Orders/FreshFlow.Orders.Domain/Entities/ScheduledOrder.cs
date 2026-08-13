using FreshFlow.Orders.Domain.Enums;
using FreshFlow.SharedKernel.Application;
using FreshFlow.SharedKernel.Domain;

namespace FreshFlow.Orders.Domain.Entities;

public sealed class ScheduledOrder : BaseEntity
{
    private readonly List<ScheduledOrderItem> _items = [];

    private ScheduledOrder() { } // EF Core

    public ScheduledOrder(
        Guid restaurantId,
        RecurrenceType recurrenceType,
        DateTime firstRunAt,
        string? notes,
        Guid? deliveryAddressId = null)
    {
        RestaurantId = restaurantId;
        RecurrenceType = recurrenceType;
        FirstRunAt = firstRunAt;
        Notes = notes;
        DeliveryAddressId = deliveryAddressId;
    }

    public Guid RestaurantId { get; private set; }
    public Guid? MarketId { get; private set; }
    public RecurrenceType RecurrenceType { get; private set; }
    public DateTime FirstRunAt { get; private set; }
    public DateTime? LastExecutedAt { get; private set; }
    public DateTime? CancelledAt { get; private set; }
    public string? Notes { get; private set; }

    /// <summary>
    /// Delivery address the background job confirms against. Nullable for backward compat —
    /// legacy schedules created before SCRUM-386 degrade to an empty Draft instead of
    /// auto-confirming (see <c>ScheduledOrderGenerationService</c>).
    /// </summary>
    public Guid? DeliveryAddressId { get; private set; }

    public bool IsActive => !CancelledAt.HasValue;

    public IReadOnlyCollection<ScheduledOrderItem> Items => _items.AsReadOnly();

    /// <summary>
    /// Records that a concrete order instance was generated from this template at <paramref name="executedAt"/>.
    /// Used by the background generation job to enforce idempotency.
    /// </summary>
    public void RecordExecution(DateTime executedAt)
    {
        LastExecutedAt = executedAt;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>Adds one line to the item template.</summary>
    public void AddItem(Guid marketProductId, int quantity) =>
        _items.Add(new ScheduledOrderItem(marketProductId, quantity));

    /// <summary>Replaces the entire item template, e.g. from <c>UpdateScheduledOrder</c>.</summary>
    public void ReplaceItems(
        IEnumerable<(Guid MarketProductId, int Quantity)> items,
        Guid? marketId = null)
    {
        _items.Clear();
        foreach (var (marketProductId, quantity) in items)
            AddItem(marketProductId, quantity);
        MarketId = _items.Count == 0 ? null : marketId ?? MarketId;
    }

    public Result AssignMarket(Guid marketId)
    {
        if (marketId == Guid.Empty)
            return Result.Failure(Error.Validation("INVALID_MARKET", "A market is required."));
        if (MarketId.HasValue && MarketId != marketId)
            return Result.Failure(Error.Validation(
                "ORDER_MARKET_MISMATCH", "A recurring order can only contain products from one market."));
        MarketId = marketId;
        return Result.Success();
    }

    public Result UpdateSchedule(
        RecurrenceType recurrenceType, DateTime firstRunAt, string? notes, Guid? deliveryAddressId = null)
    {
        if (!IsActive)
            return Result.Failure(Error.Conflict(
                "SCHEDULED_ORDER_NOT_ACTIVE", "This recurring schedule is not active."));

        RecurrenceType = recurrenceType;
        FirstRunAt = firstRunAt;
        Notes = notes;
        if (deliveryAddressId.HasValue)
            DeliveryAddressId = deliveryAddressId;
        UpdatedAt = DateTime.UtcNow;

        return Result.Success();
    }

    public Result Cancel(DateTime? cancelledAtUtc = null)
    {
        if (!IsActive)
            return Result.Failure(Error.Conflict(
                "SCHEDULED_ORDER_ALREADY_CANCELLED", "This recurring schedule is already cancelled."));

        CancelledAt = cancelledAtUtc ?? DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;

        return Result.Success();
    }
}
