using FreshFlow.Catalog.Application.Abstractions;
using FreshFlow.Catalog.Domain.Entities;
using FreshFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FreshFlow.Catalog.Infrastructure.Repositories;

internal sealed class UnitOfMeasurementRepository(AppDbContext db) : IUnitOfMeasurementRepository
{
    public Task<UnitOfMeasurement?> FindByIdAsync(Guid id, CancellationToken ct) =>
        db.Set<UnitOfMeasurement>()
            .FirstOrDefaultAsync(u => u.Id == id && u.DeletedAt == null, ct);

    public Task<bool> ExistsByNameAsync(string name, CancellationToken ct) =>
        db.Set<UnitOfMeasurement>()
            .AsNoTracking()
            .AnyAsync(u => u.Name == name && u.DeletedAt == null, ct);

    public async Task<IReadOnlyList<UnitOfMeasurement>> GetAllAsync(bool activeOnly, CancellationToken ct)
    {
        var query = db.Set<UnitOfMeasurement>()
            .AsNoTracking()
            .Where(u => u.DeletedAt == null);

        if (activeOnly)
            query = query.Where(u => u.IsActive);

        return await query
            .OrderBy(u => u.Name)
            .ToListAsync(ct);
    }

    public async Task AddAsync(UnitOfMeasurement unit, CancellationToken ct) =>
        await db.Set<UnitOfMeasurement>().AddAsync(unit, ct);

    public Task SaveChangesAsync(CancellationToken ct) =>
        db.SaveChangesAsync(ct);
}
