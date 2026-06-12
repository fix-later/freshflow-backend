using FreshFlow.Pricing.Domain.Entities;

namespace FreshFlow.Pricing.Application.Abstractions;

public interface IPriceSnapshotRepository
{
    public Task AddAsync(PriceSnapshot snapshot, CancellationToken ct);

    public Task<IReadOnlyList<PriceSnapshot>> GetByMarketProductIdAsync(
        Guid marketProductId, CancellationToken ct);

    public Task SaveChangesAsync(CancellationToken ct);
}
