using FreshFlow.SharedKernel.Domain;

namespace FreshFlow.Hub.Domain.Entities;

public sealed class HubSortingProgress : BaseEntity
{
    public const string StatusPending = "PENDING";
    public const string StatusSorted = "SORTED";

    private HubSortingProgress() { } // EF Core

    public Guid HubId { get; private set; }
    public DateOnly ServiceDate { get; private set; }
    public Guid? RouteId { get; private set; }
    public Guid OrderItemId { get; private set; }
    public decimal SortedQuantityKg { get; private set; }
    public string Status { get; private set; } = StatusPending;
    public Guid? SortedByUserId { get; private set; }
    public DateTime? SortedAt { get; private set; }

    public static HubSortingProgress Create(
        Guid hubId,
        DateOnly serviceDate,
        Guid orderItemId,
        Guid? routeId = null)
    {
        if (hubId == Guid.Empty)
            throw new ArgumentException("Hub id is required.", nameof(hubId));
        if (orderItemId == Guid.Empty)
            throw new ArgumentException("Order item id is required.", nameof(orderItemId));

        return new HubSortingProgress
        {
            HubId = hubId,
            ServiceDate = serviceDate,
            RouteId = routeId,
            OrderItemId = orderItemId,
            Status = StatusPending
        };
    }

    public void UpdateSortedQuantity(
        decimal sortedQuantityKg,
        decimal requiredQuantityKg,
        Guid sortedByUserId,
        DateTime at)
    {
        if (sortedQuantityKg <= 0m)
            throw new ArgumentOutOfRangeException(
                nameof(sortedQuantityKg), sortedQuantityKg, "Sorted quantity must be greater than zero.");
        if (requiredQuantityKg <= 0m)
            throw new ArgumentOutOfRangeException(
                nameof(requiredQuantityKg), requiredQuantityKg, "Required quantity must be greater than zero.");
        if (sortedQuantityKg < SortedQuantityKg || sortedQuantityKg > requiredQuantityKg)
            throw new ArgumentOutOfRangeException(
                nameof(sortedQuantityKg), sortedQuantityKg,
                "Sorted quantity cannot decrease or exceed the required quantity.");
        if (sortedByUserId == Guid.Empty)
            throw new ArgumentException("Sorted-by user id is required.", nameof(sortedByUserId));

        SortedQuantityKg = sortedQuantityKg;
        Status = sortedQuantityKg == requiredQuantityKg ? StatusSorted : StatusPending;
        SortedByUserId = sortedByUserId;
        SortedAt = Status == StatusSorted ? at : null;
        UpdatedAt = at;
    }
}
