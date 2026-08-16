using FreshFlow.Catalog.Domain.Entities;

namespace FreshFlow.Catalog.Application.Abstractions;

public interface IPackingCodeRepository
{
    public Task<PackingCode?> FindByIdAsync(Guid id, CancellationToken ct);
    public Task<bool> CodeExistsAsync(string code, Guid? excludeId, CancellationToken ct);
    public Task<IReadOnlyList<PackingCode>> GetPageAsync(
        bool activeOnly, int page, int pageSize, CancellationToken ct);
    public Task AddAsync(PackingCode packingCode, CancellationToken ct);
    public Task SaveChangesAsync(CancellationToken ct);
}
