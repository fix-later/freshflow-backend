using FreshFlow.Catalog.Domain.Entities;

namespace FreshFlow.Catalog.Application.Abstractions;

public interface IProductCategoryRepository
{
    public Task<ProductCategory?> FindByIdAsync(Guid id, CancellationToken ct);
    public Task<bool> ExistsByNameAsync(string name, CancellationToken ct);
    public Task<bool> HasChildrenAsync(Guid parentId, bool activeOnly, CancellationToken ct);
    public Task<IReadOnlyList<ProductCategory>> GetAllAsync(bool activeOnly, CancellationToken ct);
    public Task AddAsync(ProductCategory category, CancellationToken ct);
    public Task<bool> SaveChangesAsync(CancellationToken ct);
}
