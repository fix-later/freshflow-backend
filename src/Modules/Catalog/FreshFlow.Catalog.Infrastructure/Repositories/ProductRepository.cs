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
        {
            // EF.Functions.ILike translates to PostgreSQL ILIKE (case-insensitive LIKE).
            // The pattern is fully parameterised — no raw SQL string concatenation.
            // User-supplied %, _ and \ are escaped so they are treated as literal characters.
            var pattern = "%" + EscapeILikePattern(search) + "%";
            query = query.Where(p => EF.Functions.ILike(p.Name, pattern, "\\"));
        }

        if (!string.IsNullOrEmpty(category))
        {
            // M2: support both Guid-based and legacy string-based category filtering.
            // When the value parses as a Guid, match by CategoryId OR LegacyCategory (OR
            // covers the transition period when both columns are populated).
            // When the value is not a Guid, fall back to the original LegacyCategory
            // exact-match for backward compatibility.
            if (Guid.TryParse(category, out var categoryId))
                query = query.Where(p => p.CategoryId == categoryId || p.LegacyCategory == category);
            else
                query = query.Where(p => p.LegacyCategory == category);
        }

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(p => p.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items.AsReadOnly(), total);
    }

    /// <summary>
    /// Escapes PostgreSQL ILIKE wildcard characters in a user-supplied search term so they
    /// are treated as literal characters rather than wildcards. Backslash is escaped first
    /// to avoid double-escaping. The escape character is declared as <c>\</c> in the ILike call.
    /// </summary>
    internal static string EscapeILikePattern(string value) =>
        value.Replace("\\", "\\\\")
             .Replace("%", "\\%")
             .Replace("_", "\\_");
}
