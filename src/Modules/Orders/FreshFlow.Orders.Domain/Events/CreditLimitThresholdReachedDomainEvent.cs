using FreshFlow.Orders.Domain.Enums;
using FreshFlow.SharedKernel.Domain;

namespace FreshFlow.Orders.Domain.Events;

/// <summary>
/// Raised by <c>RestaurantCredit.Charge</c> the first time a charge pushes utilization
/// across a not-yet-alerted threshold (SCRUM-266). Anti-spam: does not re-raise while
/// utilization stays at or above the same level; re-arms only when a settlement/refund
/// drops utilization back below it (see <c>RestaurantCredit.LastAlertedLevel</c>).
/// </summary>
public sealed record CreditLimitThresholdReachedDomainEvent(
    Guid RestaurantId,
    CreditAlertLevel Level,
    decimal Utilization,
    decimal OutstandingBalance,
    decimal CreditLimit,
    DateTime OccurredAt) : IDomainEvent;
