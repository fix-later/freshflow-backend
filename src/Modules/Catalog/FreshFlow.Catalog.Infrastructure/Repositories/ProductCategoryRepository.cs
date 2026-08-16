using FreshFlow.Catalog.Application.Abstractions;
using FreshFlow.Catalog.Domain.Entities;
using FreshFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FreshFlow.Catalog.Infrastructure.Repositories;

internal sealed class ProductCategoryRepository(AppDbContext db) : IProductCategoryRepository
{
    public Task<ProductCategory?> FindByIdAsync(Guid id, CancellationToken ct) =>
        db.Set<ProductCategory>()
            .FirstOrDefaultAsync(c => c.Id == id && c.DeletedAt == null, ct);

    public Task<bool> ExistsByNameAsync(string name, CancellationToken ct) =>
        db.Set<ProductCategory>()
            .AsNoTracking()
            .AnyAsync(c => c.Name == name && c.DeletedAt == null, ct);

    public Task<bool> HasChildrenAsync(Guid parentId, bool activeOnly, CancellationToken ct) =>
        db.Set<ProductCategory>()
            .AsNoTracking()
            .AnyAsync(c => c.ParentId == parentId && c.DeletedAt == null && (!activeOnly || c.IsActive), ct);

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

    public async Task<bool> SaveChangesAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException ex) when (
            ex.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation,
                ConstraintName: "IX_product_categories_Name"
            })
        {
            return false;
        }
    }
}
