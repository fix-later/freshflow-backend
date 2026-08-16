using FreshFlow.Infrastructure.Persistence;
using FreshFlow.Logistics.Application.Abstractions;
using FreshFlow.Logistics.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FreshFlow.Logistics.Infrastructure.Repositories;

internal sealed class DeliveryRepository(AppDbContext db) : IDeliveryRepository
{
    public async Task AddRangeAsync(IReadOnlyList<Delivery> deliveries, CancellationToken ct) =>
        await db.Set<Delivery>().AddRangeAsync(deliveries, ct);

    public async Task<bool> ExistsForOrderAsync(Guid orderId, CancellationToken ct) =>
        await db.Set<Delivery>()
            .AsNoTracking()
            .AnyAsync(d => d.OrderId == orderId && d.DeletedAt == null, ct);

    public async Task<IReadOnlySet<Guid>> GetExistingOrderIdsAsync(
        IReadOnlyCollection<Guid> orderIds,
        CancellationToken ct) =>
        orderIds.Count == 0
            ? new HashSet<Guid>()
            : (await db.Set<Delivery>()
                .AsNoTracking()
                .Where(d => orderIds.Contains(d.OrderId) && d.DeletedAt == null)
                .Select(d => d.OrderId)
                .ToListAsync(ct))
            .ToHashSet();

    public Task<Delivery?> FindByIdAsync(Guid deliveryId, CancellationToken ct) =>
        db.Set<Delivery>().FirstOrDefaultAsync(d => d.Id == deliveryId, ct);

    public async Task<IReadOnlyList<Delivery>> GetByRouteIdsAsync(
        IReadOnlyCollection<Guid> routeIds,
        CancellationToken ct)
    {
        if (routeIds.Count == 0)
            return [];

        return await db.Set<Delivery>()
            .AsNoTracking()
            .Where(d => routeIds.Contains(d.DeliveryRouteId) && d.DeletedAt == null)
            .OrderBy(d => d.SequenceNumber)
            .ThenBy(d => d.Id)
            .ToListAsync(ct);
    }

    public Task SaveChangesAsync(CancellationToken ct) =>
        db.SaveChangesAsync(ct);

    public async Task<bool> TrySaveChangesAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException ex) when (DeliveryRouteRepository.IsUniqueViolation(ex))
        {
            return false;
        }
    }
}
