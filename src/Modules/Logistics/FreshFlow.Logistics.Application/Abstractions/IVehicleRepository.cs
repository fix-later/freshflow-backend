using FreshFlow.Logistics.Domain.Entities;

namespace FreshFlow.Logistics.Application.Abstractions;

public interface IVehicleRepository
{
    public Task AddAsync(Vehicle vehicle, CancellationToken ct);
    public Task SaveChangesAsync(CancellationToken ct);
    public Task<Vehicle?> FindByIdAsync(Guid id, CancellationToken ct);
    public Task<bool> PlateNumberExistsAsync(string plateNumber, Guid? excludeId, CancellationToken ct);
    public Task<(IReadOnlyList<Vehicle> Items, string? NextCursor)> GetPageAsync(
        string? cursor,
        int pageSize,
        bool? isActive,
        Guid? hubId,
        CancellationToken ct);
}
