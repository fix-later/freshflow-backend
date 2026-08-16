using MediatR;

namespace FreshFlow.Contracts;

/// <summary>
/// A restaurant's credit charge crossed a not-yet-alerted utilization threshold
/// (SCRUM-266). <c>Level</c> is snake_case: <c>"warning"</c> (80-99% utilization) or
/// <c>"exceeded"</c> (&gt;=100%).
/// </summary>
public sealed record CreditLimitThresholdReachedIntegrationEvent(
    Guid RestaurantId,
    string Level,
    decimal Utilization,
    decimal OutstandingBalance,
    decimal CreditLimit,
    DateTime OccurredAt) : INotification;
