using FreshFlow.Orders.Domain.Enums;
using FreshFlow.SharedKernel.Application;
using FreshFlow.SharedKernel.Domain;

namespace FreshFlow.Orders.Domain.Entities;

public sealed class ScheduledOrder : BaseEntity
{
    private ScheduledOrder() { } // EF Core

    public ScheduledOrder(Guid restaurantId, RecurrenceType recurrenceType, DateTime firstRunAt, string? notes)
    {
        RestaurantId = restaurantId;
        RecurrenceType = recurrenceType;
        FirstRunAt = firstRunAt;
        Notes = notes;
    }

    public Guid RestaurantId { get; private set; }
    public RecurrenceType RecurrenceType { get; private set; }
    public DateTime FirstRunAt { get; private set; }
    public DateTime? LastExecutedAt { get; private set; }
    public DateTime? CancelledAt { get; private set; }
    public string? Notes { get; private set; }

    public bool IsActive => !CancelledAt.HasValue;

    /// <summary>
    /// Records that a concrete order instance was generated from this template at <paramref name="executedAt"/>.
    /// Used by the background generation job to enforce idempotency.
    /// </summary>
    public void RecordExecution(DateTime executedAt) => LastExecutedAt = executedAt;

    public Result Cancel()
    {
        if (!IsActive)
            return Result.Failure(Error.Conflict(
                "SCHEDULED_ORDER_ALREADY_CANCELLED", "This recurring schedule is already cancelled."));

        CancelledAt = DateTime.UtcNow;
        return Result.Success();
    }
}
