using MediatR;

namespace FreshFlow.Contracts;

/// <summary>
/// A recurring schedule's background auto-confirm attempt (SCRUM-386) failed or had no item
/// template to confirm from — the generated order was left as a Draft the restaurant must edit
/// and confirm by hand.
/// </summary>
public sealed record ScheduledOrderNeedsAttentionIntegrationEvent(
    Guid ScheduledOrderId,
    Guid OrderId,
    Guid RestaurantId,
    DateTime OccurrenceDate,
    string Reason,
    DateTime OccurredAt) : INotification;
