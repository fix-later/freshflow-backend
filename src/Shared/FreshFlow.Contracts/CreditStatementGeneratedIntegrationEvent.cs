using MediatR;

namespace FreshFlow.Contracts;

/// <summary>
/// A restaurant's monthly credit statement was newly generated (SCRUM-269) — raised once, on
/// first generation only, never on an idempotent re-generate. Consumed by Notifications to
/// notify the restaurant owner in-app and by email. <c>DueDate</c> is the soft payment
/// reminder date (period end + configured term); there is no paid/overdue lifecycle.
/// <c>StatementPdf</c> carries the rendered statement (SCRUM-364) so Notifications can attach
/// it to the email without referencing Orders — it is null when rendering failed, in which
/// case the email still sends unattached.
/// </summary>
public sealed record CreditStatementGeneratedIntegrationEvent(
    Guid RestaurantId,
    Guid StatementId,
    DateTime PeriodStart,
    DateTime PeriodEnd,
    decimal ClosingBalance,
    DateTime DueDate,
    DateTime GeneratedAt,
    byte[]? StatementPdf = null,
    string? StatementPdfFileName = null) : INotification;
