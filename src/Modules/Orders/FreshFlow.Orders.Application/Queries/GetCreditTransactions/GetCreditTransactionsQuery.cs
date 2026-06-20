using FreshFlow.Orders.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Orders.Application.Queries.GetCreditTransactions;

public sealed record GetCreditTransactionsQuery(
    Guid UserId,
    bool IsAdmin,
    Guid RestaurantId) : IQuery<IReadOnlyList<CreditTransactionDto>>;
