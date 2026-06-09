using FreshFlow.Catalog.Domain.Entities;

namespace FreshFlow.Catalog.Application.Abstractions;

public interface IMarketRepository
{
    public Task<Market?> FindByIdAsync(Guid id, CancellationToken ct);
    public Task<IReadOnlyList<Market>> GetAllAsync(bool activeOnly, CancellationToken ct);
    public Task AddAsync(Market market, CancellationToken ct);
    public Task SaveChangesAsync(CancellationToken ct);
}
