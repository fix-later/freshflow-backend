using FreshFlow.Catalog.Domain.Entities;

namespace FreshFlow.Catalog.Application.Abstractions;

public interface IProductRepository
{
    public Task<Product?> FindByIdAsync(Guid id, CancellationToken ct);
    public Task AddAsync(Product product, CancellationToken ct);
    public Task SaveChangesAsync(CancellationToken ct);
    public Task<(IReadOnlyList<Product> Items, int Total)> GetPagedAsync(
        string? search, string? category, bool includeInactive,
        int page, int pageSize, CancellationToken ct);
}
