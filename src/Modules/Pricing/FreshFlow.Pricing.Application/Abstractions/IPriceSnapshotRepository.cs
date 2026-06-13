using FreshFlow.Pricing.Domain.Entities;

namespace FreshFlow.Pricing.Application.Abstractions;

public interface IPriceSnapshotRepository
{
    public Task AddAsync(PriceSnapshot snapshot, CancellationToken ct);

    public Task<IReadOnlyList<PriceSnapshot>> GetByMarketProductIdAsync(
        Guid marketProductId, CancellationToken ct);

    /// <summary>
    /// Returns a cursor-paginated page of price snapshots for a market product,
    /// ordered by <c>RecordedAt</c> descending (newest first).
    /// </summary>
    /// <param name="marketProductId">The market product to query.</param>
    /// <param name="cursor">
    /// Opaque composite cursor from the previous page response; <c>null</c> for the first page.
    /// Encodes <c>(RecordedAt, Id)</c> of the last item on the previous page as base64 JSON.
    /// </param>
    /// <param name="pageSize">Maximum items per page (1–200).</param>
    /// <param name="from">Inclusive lower bound on <c>RecordedAt</c> (UTC); <c>null</c> = no lower bound.</param>
    /// <param name="to">Inclusive upper bound on <c>RecordedAt</c> (UTC); <c>null</c> = no upper bound.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// A tuple of the page items and the next-page cursor (<c>null</c> when no more pages).
    /// </returns>
    public Task<(IReadOnlyList<PriceSnapshot> Items, string? NextCursor)> GetPageAsync(
        Guid marketProductId,
        string? cursor,
        int pageSize,
        DateTime? from,
        DateTime? to,
        CancellationToken ct);

    public Task SaveChangesAsync(CancellationToken ct);
}
