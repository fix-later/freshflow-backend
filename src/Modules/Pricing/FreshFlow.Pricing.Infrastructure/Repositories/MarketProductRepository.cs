using FreshFlow.Infrastructure.Persistence;
using FreshFlow.Pricing.Application.Abstractions;
using FreshFlow.Pricing.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FreshFlow.Pricing.Infrastructure.Repositories;

internal sealed class MarketProductRepository(AppDbContext db) : IMarketProductRepository
{
    public Task<MarketProduct?> FindByIdAsync(Guid id, CancellationToken ct) =>
        db.Set<MarketProduct>()
            .AsNoTracking()
            .FirstOrDefaultAsync(mp => mp.Id == id && mp.DeletedAt == null, ct);

    public Task<MarketProduct?> FindByMarketAndProductAsync(
        Guid marketId, Guid productId, CancellationToken ct) =>
        db.Set<MarketProduct>()
            .AsNoTracking()
            .FirstOrDefaultAsync(
                mp => mp.MarketId == marketId
                      && mp.ProductId == productId
                      && mp.DeletedAt == null,
                ct);

    public async Task<IReadOnlyList<MarketProduct>> GetByMarketIdAsync(
        Guid marketId, CancellationToken ct) =>
        await db.Set<MarketProduct>()
            .AsNoTracking()
            .Where(mp => mp.MarketId == marketId && mp.DeletedAt == null)
            .OrderBy(mp => mp.ProductId)
            .ToListAsync(ct);

    public async Task AddAsync(MarketProduct marketProduct, CancellationToken ct) =>
        await db.Set<MarketProduct>().AddAsync(marketProduct, ct);

    public async Task AddAndSaveAsync(MarketProduct marketProduct, CancellationToken ct)
    {
        await db.Set<MarketProduct>().AddAsync(marketProduct, ct);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
            when (ex.InnerException is PostgresException pg && pg.SqlState == "23505")
        {
            throw new DuplicateMarketProductException(
                "This product is already listed at this market.", ex);
        }
    }

    public void Track(MarketProduct marketProduct) => db.Update(marketProduct);

    public async Task SaveChangesAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConcurrencyConflictException(
                "The record was updated by another user. Please refresh and retry.", ex);
        }
    }

}
