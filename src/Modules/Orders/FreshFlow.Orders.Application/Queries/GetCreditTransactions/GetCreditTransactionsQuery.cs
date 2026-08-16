using FreshFlow.Orders.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Orders.Application.Queries.GetCreditTransactions;

/// <summary>
/// Returns a cursor-paginated, optionally period-filtered credit transaction (balance
/// ledger) history for a restaurant.
/// Endpoint: GET /api/v1/restaurants/{restaurantId}/credit/transactions [admin or owning restaurant].
/// </summary>
public sealed record GetCreditTransactionsQuery(
    Guid UserId,
    bool IsAdmin,
    Guid RestaurantId,
    string? Cursor = null,
    int PageSize = 50,
    DateTime? From = null,
    DateTime? To = null) : IQuery<CreditTransactionPageDto>;
