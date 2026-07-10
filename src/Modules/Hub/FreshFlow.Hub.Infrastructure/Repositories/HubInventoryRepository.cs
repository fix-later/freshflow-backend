using FreshFlow.Hub.Application.Abstractions;
using FreshFlow.Hub.Domain.Entities;
using FreshFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FreshFlow.Hub.Infrastructure.Repositories;

internal sealed class HubInventoryRepository(AppDbContext db) : IHubInventoryRepository
{
    public Task<HubInventory?> FindByHubAndMarketProductAsync(
        Guid hubId,
        Guid marketProductId,
        CancellationToken ct) =>
        db.Set<HubInventory>()
            .FirstOrDefaultAsync(i =>
                i.HubId == hubId &&
                i.MarketProductId == marketProductId &&
                i.DeletedAt == null,
                ct);

    public async Task AddAsync(HubInventory inventory, CancellationToken ct) =>
        await db.Set<HubInventory>().AddAsync(inventory, ct);
}
