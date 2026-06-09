using FreshFlow.Catalog.Application.Abstractions;
using FreshFlow.Catalog.Domain.Entities;
using FreshFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FreshFlow.Catalog.Infrastructure.Repositories;

internal sealed class ProductCategoryRepository(AppDbContext db) : IProductCategoryRepository
{
    public Task<ProductCategory?> FindByIdAsync(Guid id, CancellationToken ct) =>
        db.Set<ProductCategory>()
            .FirstOrDefaultAsync(c => c.Id == id && c.DeletedAt == null, ct);

    public Task<bool> ExistsByNameAsync(string name, CancellationToken ct) =>
        db.Set<ProductCategory>()
            .AsNoTracking()
            .AnyAsync(c => c.Name == name && c.IsActive && c.DeletedAt == null, ct);

    public async Task<IReadOnlyList<ProductCategory>> GetAllAsync(bool activeOnly, CancellationToken ct)
    {
        var query = db.Set<ProductCategory>()
            .AsNoTracking()
            .Where(c => c.DeletedAt == null);

        if (activeOnly)
            query = query.Where(c => c.IsActive);

        return await query
            .OrderBy(c => c.Name)
            .ToListAsync(ct);
    }

    public async Task AddAsync(ProductCategory category, CancellationToken ct) =>
        await db.Set<ProductCategory>().AddAsync(category, ct);

    public Task SaveChangesAsync(CancellationToken ct) =>
        db.SaveChangesAsync(ct);
}
