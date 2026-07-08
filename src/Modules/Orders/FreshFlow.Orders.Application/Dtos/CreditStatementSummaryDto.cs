namespace FreshFlow.Orders.Application.Dtos;

/// <summary>Statement header without line items — used for the paginated list view.</summary>
public sealed record CreditStatementSummaryDto(
    Guid Id,
    Guid RestaurantId,
    DateTime PeriodStart,
    DateTime PeriodEnd,
    decimal OpeningBalance,
    decimal ClosingBalance,
    decimal TotalCharges,
    decimal TotalSettlements,
    decimal TotalRefunds,
    DateTime GeneratedAt);
