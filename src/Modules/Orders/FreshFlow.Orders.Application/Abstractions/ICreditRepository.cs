using FreshFlow.Orders.Domain.Entities;

namespace FreshFlow.Orders.Application.Abstractions;

public interface ICreditRepository
{
    public Task<RestaurantCredit?> FindAccountAsync(Guid restaurantId, CancellationToken ct);
    public Task AddAccountAsync(RestaurantCredit account, CancellationToken ct);
    public void Track(RestaurantCredit account);
    public void AddTransaction(CreditTransaction transaction);
    public Task<decimal> GetRefundableAmountForOrderAsync(
        Guid orderId,
        CancellationToken ct);

    /// <summary>
    /// Returns a cursor-paginated page of balance-moving credit transactions (charge,
    /// settlement, refund) for a restaurant, ordered by <c>CreatedAt</c> descending
    /// (newest first). Adjustment rows are excluded defensively — a credit-limit change
    /// is not a balance movement.
    /// </summary>
    /// <param name="restaurantId">The restaurant whose ledger is queried.</param>
    /// <param name="cursor">
    /// Opaque composite cursor from the previous page response; <c>null</c> for the first page.
    /// Encodes <c>(CreatedAt, Id)</c> of the last item on the previous page as base64 JSON.
    /// </param>
    /// <param name="pageSize">Maximum items per page (1-200).</param>
    /// <param name="from">Inclusive lower bound on <c>CreatedAt</c> (UTC); <c>null</c> = no lower bound.</param>
    /// <param name="to">Inclusive upper bound on <c>CreatedAt</c> (UTC); <c>null</c> = no upper bound.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// A tuple of the page items and the next-page cursor (<c>null</c> when no more pages).
    /// </returns>
    public Task<(IReadOnlyList<CreditTransaction> Items, string? NextCursor)> GetTransactionsPageAsync(
        Guid restaurantId,
        string? cursor,
        int pageSize,
        DateTime? from,
        DateTime? to,
        CancellationToken ct);

    /// <summary>
    /// Returns ALL balance-moving transactions for a restaurant within
    /// <c>[from, toExclusive)</c>, ordered chronologically (<c>CreatedAt</c> ascending, then
    /// <c>Id</c>). Unbounded by design — used to build a statement snapshot for a single
    /// month, which is naturally bounded in volume.
    /// </summary>
    public Task<IReadOnlyList<CreditTransaction>> GetTransactionsInPeriodAsync(
        Guid restaurantId,
        DateTime from,
        DateTime toExclusive,
        CancellationToken ct);

    /// <summary>
    /// Returns the net balance movement (sum of Charge minus Settlement/Refund amounts) for
    /// a restaurant strictly before <paramref name="beforeExclusive"/>. Used as the opening
    /// balance fallback for a restaurant's very first statement, when no prior statement
    /// exists to carry a closing balance forward from.
    /// </summary>
    public Task<decimal> GetNetBalanceMovementBeforeAsync(
        Guid restaurantId, DateTime beforeExclusive, CancellationToken ct);

    /// <summary>Returns the restaurant ids of all restaurants that have a credit account.</summary>
    public Task<IReadOnlyList<Guid>> GetActiveRestaurantIdsAsync(CancellationToken ct);

    public Task SaveChangesAsync(CancellationToken ct);
}
