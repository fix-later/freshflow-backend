using FreshFlow.Pricing.Domain.Entities;

namespace FreshFlow.Pricing.Application.Abstractions;

public interface IMarketProductRepository
{
    public Task<MarketProduct?> FindByIdAsync(Guid id, CancellationToken ct);

    public Task<MarketProduct?> FindByMarketAndProductAsync(
        Guid marketId, Guid productId, CancellationToken ct);

    public Task<IReadOnlyList<MarketProduct>> GetByMarketIdAsync(
        Guid marketId, CancellationToken ct);

    public Task AddAsync(MarketProduct marketProduct, CancellationToken ct);

    /// <summary>
    /// Re-attaches a detached entity to the change tracker so mutations are persisted.
    /// </summary>
    public void Track(MarketProduct marketProduct);

    public Task SaveChangesAsync(CancellationToken ct);
}
