using FreshFlow.Catalog.Domain.Entities;

namespace FreshFlow.Catalog.Application.Abstractions;

public interface IMarketRepository
{
    public Task<Market?> FindByIdAsync(Guid id, CancellationToken ct);
    public Task<IReadOnlyList<Market>> GetAllAsync(bool activeOnly, CancellationToken ct);
    public Task AddAsync(Market market, CancellationToken ct);

    /// <summary>
    /// Re-attaches a detached entity (loaded via <see cref="FindByIdAsync"/>) to the
    /// change tracker so that subsequent mutations are persisted by <see cref="SaveChangesAsync"/>.
    /// Call this in command handlers after mutating the entity.
    /// </summary>
    public void Track(Market market);

    public Task SaveChangesAsync(CancellationToken ct);
}
