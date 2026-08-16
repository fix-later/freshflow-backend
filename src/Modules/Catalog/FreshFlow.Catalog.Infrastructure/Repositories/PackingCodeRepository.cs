using FreshFlow.Catalog.Application.Abstractions;
using FreshFlow.Catalog.Domain.Entities;
using FreshFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FreshFlow.Catalog.Infrastructure.Repositories;

internal sealed class PackingCodeRepository(AppDbContext db) : IPackingCodeRepository
{
    public Task<PackingCode?> FindByIdAsync(Guid id, CancellationToken ct) =>
        db.Set<PackingCode>()
            .FirstOrDefaultAsync(x => x.Id == id && x.DeletedAt == null, ct);

    public Task<bool> CodeExistsAsync(string code, Guid? excludeId, CancellationToken ct) =>
        db.Set<PackingCode>()
            .AsNoTracking()
            .AnyAsync(x => x.Code == code && x.DeletedAt == null
                && (!excludeId.HasValue || x.Id != excludeId.Value), ct);

    public async Task<IReadOnlyList<PackingCode>> GetPageAsync(
        bool activeOnly, int page, int pageSize, CancellationToken ct)
    {
        var query = db.Set<PackingCode>()
            .AsNoTracking()
            .Where(x => x.DeletedAt == null);

        if (activeOnly)
            query = query.Where(x => x.IsActive);

        return await query
            .OrderBy(x => x.Code)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
    }

    public async Task AddAsync(PackingCode packingCode, CancellationToken ct) =>
        await db.Set<PackingCode>().AddAsync(packingCode, ct);

    public Task SaveChangesAsync(CancellationToken ct) =>
        db.SaveChangesAsync(ct);
}
