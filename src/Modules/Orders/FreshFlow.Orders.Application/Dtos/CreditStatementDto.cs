namespace FreshFlow.Orders.Application.Dtos;

/// <summary>Full statement detail, including line items — returned by generate/get-by-id.</summary>
public sealed record CreditStatementDto(
    Guid Id,
    Guid RestaurantId,
    DateTime PeriodStart,
    DateTime PeriodEnd,
    decimal OpeningBalance,
    decimal ClosingBalance,
    decimal TotalCharges,
    decimal TotalSettlements,
    decimal TotalRefunds,
    DateTime GeneratedAt,
    DateTime DueDate,
    IReadOnlyList<CreditStatementLineDto> Lines);
