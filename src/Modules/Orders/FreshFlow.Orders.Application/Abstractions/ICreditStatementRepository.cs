using FreshFlow.Orders.Domain.Entities;

namespace FreshFlow.Orders.Application.Abstractions;

public interface ICreditStatementRepository
{
    /// <summary>
    /// Returns the statement for the given (restaurant, period start), or <c>null</c> if
    /// that period has not been generated yet. <c>PeriodStart</c> together with
    /// <c>RestaurantId</c> uniquely identifies a statement.
    /// </summary>
    public Task<CreditStatement?> FindByPeriodAsync(Guid restaurantId, DateTime periodStart, CancellationToken ct);

    /// <summary>Returns a single statement by id, including its line items, or <c>null</c>.</summary>
    public Task<CreditStatement?> FindByIdAsync(Guid statementId, CancellationToken ct);

    public Task AddAsync(CreditStatement statement, CancellationToken ct);

    /// <summary>
    /// Returns a cursor-paginated page of statement summaries (no line items) for a
    /// restaurant, ordered by <c>PeriodStart</c> descending (most recent period first).
    /// </summary>
    public Task<(IReadOnlyList<CreditStatement> Items, string? NextCursor)> GetPageAsync(
        Guid restaurantId,
        string? cursor,
        int pageSize,
        CancellationToken ct);

    public Task SaveChangesAsync(CancellationToken ct);
}
