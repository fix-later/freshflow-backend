using FreshFlow.Infrastructure.Persistence;
using FreshFlow.Logistics.Application.Abstractions;
using FreshFlow.Logistics.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FreshFlow.Logistics.Infrastructure.Repositories;

internal sealed class DeliveryZoneRepository(AppDbContext db) : IDeliveryZoneRepository
{
    public Task<DeliveryZone?> FindByIdAsync(Guid id, CancellationToken ct) =>
        db.Set<DeliveryZone>().FirstOrDefaultAsync(z => z.Id == id, ct);

    public Task<bool> CodeExistsAsync(string code, CancellationToken ct)
    {
        var normalized = code.Trim().ToUpperInvariant();
        return db.Set<DeliveryZone>()
            .AsNoTracking()
            .AnyAsync(z => z.Code == normalized && z.DeletedAt == null, ct);
    }

    public async Task<IReadOnlyList<DeliveryZone>> GetAllAsync(bool activeOnly, CancellationToken ct)
    {
        var query = db.Set<DeliveryZone>().AsNoTracking();

        if (activeOnly)
            query = query.Where(z => z.IsActive && z.DeletedAt == null);

        return await query
            .OrderBy(z => z.Code)
            .ToListAsync(ct);
    }

    public async Task AddAsync(DeliveryZone zone, CancellationToken ct) =>
        await db.Set<DeliveryZone>().AddAsync(zone, ct);

    public Task SaveChangesAsync(CancellationToken ct) =>
        db.SaveChangesAsync(ct);
}
