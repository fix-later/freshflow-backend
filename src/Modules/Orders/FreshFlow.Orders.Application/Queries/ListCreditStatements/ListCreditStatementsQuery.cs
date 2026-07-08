using FreshFlow.Orders.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Orders.Application.Queries.ListCreditStatements;

/// <summary>
/// Returns a cursor-paginated list of credit statement summaries (no line items) for a
/// restaurant, most recent period first.
/// Endpoint: GET /api/v1/restaurants/{restaurantId}/credit/statements [admin or owning restaurant].
/// </summary>
public sealed record ListCreditStatementsQuery(
    Guid UserId,
    bool IsAdmin,
    Guid RestaurantId,
    string? Cursor = null,
    int PageSize = 50) : IQuery<CreditStatementPageDto>;
