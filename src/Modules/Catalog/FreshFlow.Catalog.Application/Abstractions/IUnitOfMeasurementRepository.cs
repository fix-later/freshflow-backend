using FreshFlow.Catalog.Domain.Entities;

namespace FreshFlow.Catalog.Application.Abstractions;

public interface IUnitOfMeasurementRepository
{
    public Task<UnitOfMeasurement?> FindByIdAsync(Guid id, CancellationToken ct);
    public Task<bool> ExistsByNameAsync(string name, CancellationToken ct);
    public Task<IReadOnlyList<UnitOfMeasurement>> GetAllAsync(bool activeOnly, CancellationToken ct);
    public Task AddAsync(UnitOfMeasurement unit, CancellationToken ct);
    public Task SaveChangesAsync(CancellationToken ct);
}
