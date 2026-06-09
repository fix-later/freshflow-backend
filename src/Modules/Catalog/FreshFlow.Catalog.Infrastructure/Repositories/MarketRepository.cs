using FreshFlow.Catalog.Application.Abstractions;
using FreshFlow.Catalog.Domain.Entities;
using FreshFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FreshFlow.Catalog.Infrastructure.Repositories;

internal sealed class MarketRepository(AppDbContext db) : IMarketRepository
{
    public Task<Market?> FindByIdAsync(Guid id, CancellationToken ct) =>
        db.Set<Market>()
            .FirstOrDefaultAsync(m => m.Id == id && m.DeletedAt == null, ct);

    public async Task<IReadOnlyList<Market>> GetAllAsync(bool activeOnly, CancellationToken ct)
    {
        var query = db.Set<Market>()
            .AsNoTracking()
            .Where(m => m.DeletedAt == null);

        if (activeOnly)
            query = query.Where(m => m.IsActive);

        return await query
            .OrderBy(m => m.Name)
            .ToListAsync(ct);
    }

    public async Task AddAsync(Market market, CancellationToken ct) =>
        await db.Set<Market>().AddAsync(market, ct);

    public Task SaveChangesAsync(CancellationToken ct) =>
        db.SaveChangesAsync(ct);
}
