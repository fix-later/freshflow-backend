using FreshFlow.Logistics.Domain.Entities;

namespace FreshFlow.Logistics.Application.Abstractions;

public interface IDeliveryZoneRepository
{
    public Task<DeliveryZone?> FindByIdAsync(Guid id, CancellationToken ct);
    public Task<bool> CodeExistsAsync(string code, CancellationToken ct);
    public Task<IReadOnlyList<DeliveryZone>> GetAllAsync(bool activeOnly, CancellationToken ct);
    public Task AddAsync(DeliveryZone zone, CancellationToken ct);
    public Task SaveChangesAsync(CancellationToken ct);
}
