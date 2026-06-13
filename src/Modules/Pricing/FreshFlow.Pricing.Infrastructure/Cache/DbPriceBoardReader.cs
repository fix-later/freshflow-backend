using FreshFlow.Infrastructure.Persistence;
using FreshFlow.Pricing.Application.Abstractions;
using FreshFlow.Pricing.Application.Dtos;
using FreshFlow.Pricing.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FreshFlow.Pricing.Infrastructure.Cache;

/// <summary>
/// DB-direct implementation of <see cref="IPriceBoardReader"/> (v1 — UC-PRI-09).
///
/// Reads live price/quantity directly from <c>market_products</c>, making the price board
/// always consistent with the last committed price update without requiring Redis.
///
/// TODO: replace with a Redis-backed reader when ready — see UC-PRI-07.
///       The interface is designed so only this class needs to change; the caller
///       (<c>GetMarketProductsQueryHandler</c>) is untouched.
/// </summary>
internal sealed class DbPriceBoardReader(AppDbContext db) : IPriceBoardReader
{
    public async Task<IReadOnlyDictionary<Guid, LivePriceEntry>> GetBatchAsync(
        Guid marketId,
        IReadOnlyList<Guid> productIds,
        CancellationToken ct = default)
    {
        if (productIds.Count == 0)
            return new Dictionary<Guid, LivePriceEntry>();

        var rows = await db.Set<MarketProduct>()
            .Where(mp => mp.MarketId == marketId
                         && productIds.Contains(mp.ProductId)
                         && mp.DeletedAt == null)
            .Select(mp => new { mp.ProductId, mp.CurrentPrice, mp.CurrentQuantity })
            .ToListAsync(ct);

        return rows.ToDictionary(
            r => r.ProductId,
            r => new LivePriceEntry(
                Price: r.CurrentPrice,
                Quantity: r.CurrentQuantity,
                // TODO: subtract soft-reserved from Redis order:reservation:{marketProductId}
                // when the Orders module reservation counter is integrated.
                // For v1, AvailableQuantity = Quantity (no soft-reservation source yet).
                AvailableQuantity: r.CurrentQuantity));
    }
}
