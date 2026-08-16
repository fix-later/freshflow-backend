using FreshFlow.Hub.Application.Abstractions;
using FreshFlow.Hub.Domain.Entities;

namespace FreshFlow.Hub.UnitTests.TestDoubles;

internal sealed class InMemoryHubInventoryRepository : IHubInventoryRepository
{
    private readonly List<HubInventory> _inventory = [];

    public IReadOnlyList<HubInventory> Inventory => _inventory.AsReadOnly();

    public Task<HubInventory?> FindByHubAndMarketProductAsync(
        Guid hubId,
        Guid marketProductId,
        CancellationToken ct) =>
        Task.FromResult(_inventory.FirstOrDefault(i =>
            i.HubId == hubId &&
            i.MarketProductId == marketProductId &&
            i.DeletedAt == null));

    public Task AddAsync(HubInventory inventory, CancellationToken ct)
    {
        _inventory.Add(inventory);
        return Task.CompletedTask;
    }
}
