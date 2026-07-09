using FreshFlow.Logistics.Application.Abstractions;
using FreshFlow.Logistics.Domain.Entities;

namespace FreshFlow.Logistics.UnitTests.TestDoubles;

internal sealed class InMemoryDeliveryZoneRepository : IDeliveryZoneRepository
{
    private readonly List<DeliveryZone> _zones = [];

    public IReadOnlyList<DeliveryZone> Zones => _zones.AsReadOnly();
    public int SaveChangesCount { get; private set; }

    public Task<DeliveryZone?> FindByIdAsync(Guid id, CancellationToken ct) =>
        Task.FromResult(_zones.FirstOrDefault(z => z.Id == id));

    public Task<bool> CodeExistsAsync(string code, CancellationToken ct)
    {
        var normalized = code.Trim().ToUpperInvariant();
        var exists = _zones.Any(z => z.Code == normalized && z.DeletedAt is null);
        return Task.FromResult(exists);
    }

    public Task<IReadOnlyList<DeliveryZone>> GetAllAsync(bool activeOnly, CancellationToken ct)
    {
        var query = _zones.AsEnumerable();
        if (activeOnly)
            query = query.Where(z => z.IsActive && z.DeletedAt is null);

        return Task.FromResult<IReadOnlyList<DeliveryZone>>(
            query.OrderBy(z => z.Code).ToList().AsReadOnly());
    }

    public Task AddAsync(DeliveryZone zone, CancellationToken ct)
    {
        _zones.Add(zone);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken ct)
    {
        SaveChangesCount++;
        return Task.CompletedTask;
    }
}
