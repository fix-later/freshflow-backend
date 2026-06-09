using FreshFlow.Catalog.Application.Abstractions;
using FreshFlow.Catalog.Domain.Entities;
using FreshFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FreshFlow.Catalog.Infrastructure.Repositories;

internal sealed class ProductRepository(AppDbContext db) : IProductRepository
{
    public Task<Product?> FindByIdAsync(Guid id, CancellationToken ct) =>
        db.Set<Product>()
            .FirstOrDefaultAsync(p => p.Id == id && p.DeletedAt == null, ct);

    public async Task AddAsync(Product product, CancellationToken ct) =>
        await db.Set<Product>().AddAsync(product, ct);

    public Task SaveChangesAsync(CancellationToken ct) =>
        db.SaveChangesAsync(ct);

    public async Task<(IReadOnlyList<Product> Items, int Total)> GetPagedAsync(
        string? search, string? category, bool includeInactive,
        int page, int pageSize, CancellationToken ct)
    {
        var query = db.Set<Product>().AsNoTracking();

        if (!includeInactive)
            query = query.Where(p => p.DeletedAt == null);

        if (!string.IsNullOrEmpty(search))
            query = query.Where(p => p.Name.Contains(search));

        if (!string.IsNullOrEmpty(category))
            query = query.Where(p => p.LegacyCategory == category);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(p => p.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items.AsReadOnly(), total);
    }
}
