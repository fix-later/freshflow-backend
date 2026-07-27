using FreshFlow.SharedKernel.Domain;

namespace FreshFlow.Hub.Domain.Entities;

public sealed class HubSortingProgress : BaseEntity
{
    public const string StatusPending = "PENDING";
    public const string StatusSorted = "SORTED";

    private HubSortingProgress() { } // EF Core

    public Guid RouteId { get; private set; }
    public Guid OrderItemId { get; private set; }
    public decimal SortedQuantityKg { get; private set; }
    public string Status { get; private set; } = StatusPending;
    public Guid? SortedByUserId { get; private set; }
    public DateTime? SortedAt { get; private set; }

    public static HubSortingProgress Create(Guid routeId, Guid orderItemId)
    {
        if (routeId == Guid.Empty)
            throw new ArgumentException("Route id is required.", nameof(routeId));
        if (orderItemId == Guid.Empty)
            throw new ArgumentException("Order item id is required.", nameof(orderItemId));

        return new HubSortingProgress
        {
            RouteId = routeId,
            OrderItemId = orderItemId,
            Status = StatusPending
        };
    }

    // Idempotent by design: calling this again (e.g. a re-sort correction) just overwrites the
    // quantity/who/when for the same (RouteId, OrderItemId) row instead of throwing.
    public void MarkSorted(decimal sortedQuantityKg, Guid sortedByUserId, DateTime at)
    {
        if (sortedQuantityKg <= 0m)
            throw new ArgumentOutOfRangeException(
                nameof(sortedQuantityKg), sortedQuantityKg, "Sorted quantity must be greater than zero.");
        if (sortedByUserId == Guid.Empty)
            throw new ArgumentException("Sorted-by user id is required.", nameof(sortedByUserId));

        SortedQuantityKg = sortedQuantityKg;
        Status = StatusSorted;
        SortedByUserId = sortedByUserId;
        SortedAt = at;
        UpdatedAt = at;
    }
}
