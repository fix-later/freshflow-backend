using FreshFlow.Infrastructure.Persistence;
using FreshFlow.Pricing.Application.Abstractions;
using FreshFlow.Pricing.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FreshFlow.Pricing.Infrastructure.Repositories;

internal sealed class PriceSnapshotRepository(AppDbContext db) : IPriceSnapshotRepository
{
    public async Task AddAsync(PriceSnapshot snapshot, CancellationToken ct) =>
        await db.Set<PriceSnapshot>().AddAsync(snapshot, ct);

    public async Task<IReadOnlyList<PriceSnapshot>> GetByMarketProductIdAsync(
        Guid marketProductId, CancellationToken ct) =>
        await db.Set<PriceSnapshot>()
            .AsNoTracking()
            .Where(ps => ps.MarketProductId == marketProductId)
            .OrderByDescending(ps => ps.RecordedAt)
            .ToListAsync(ct);

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
