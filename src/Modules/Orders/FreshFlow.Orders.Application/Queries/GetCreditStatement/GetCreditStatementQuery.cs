using FreshFlow.Orders.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Orders.Application.Queries.GetCreditStatement;

/// <summary>
/// Returns a single credit statement, looked up either by <c>StatementId</c> or by
/// (<c>Year</c>, <c>Month</c>) — exactly one lookup mode must be provided.
/// Endpoint: GET /api/v1/restaurants/{restaurantId}/credit/statements/{statementId} [admin or owning restaurant].
/// </summary>
public sealed record GetCreditStatementQuery(
    Guid UserId,
    bool IsAdmin,
    Guid RestaurantId,
    Guid? StatementId = null,
    int? Year = null,
    int? Month = null) : IQuery<CreditStatementDto>;
