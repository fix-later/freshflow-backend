using FreshFlow.Hub.Domain.Entities;

namespace FreshFlow.Hub.Application.Abstractions;

public interface IHubInventoryRepository
{
    public Task<HubInventory?> FindByHubAndMarketProductAsync(
        Guid hubId,
        Guid marketProductId,
        CancellationToken ct);

    public Task AddAsync(HubInventory inventory, CancellationToken ct);
}
