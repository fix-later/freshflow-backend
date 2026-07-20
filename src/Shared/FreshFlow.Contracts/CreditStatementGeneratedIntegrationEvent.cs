using MediatR;

namespace FreshFlow.Contracts;

/// <summary>
/// A restaurant's monthly credit statement was newly generated (SCRUM-269) — raised once, on
/// first generation only, never on an idempotent re-generate. Consumed by Notifications to
/// notify the restaurant owner in-app and by email. <c>DueDate</c> is the soft payment
/// reminder date (period end + configured term); there is no paid/overdue lifecycle.
/// </summary>
public sealed record CreditStatementGeneratedIntegrationEvent(
    Guid RestaurantId,
    Guid StatementId,
    DateTime PeriodStart,
    DateTime PeriodEnd,
    decimal ClosingBalance,
    DateTime DueDate,
    DateTime GeneratedAt) : INotification;
