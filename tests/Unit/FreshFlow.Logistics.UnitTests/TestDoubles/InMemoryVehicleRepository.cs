using FreshFlow.Logistics.Application.Abstractions;
using FreshFlow.Logistics.Domain.Entities;

namespace FreshFlow.Logistics.UnitTests.TestDoubles;

internal sealed class InMemoryVehicleRepository : IVehicleRepository
{
    private readonly List<Vehicle> _vehicles = [];

    public IReadOnlyList<Vehicle> Vehicles => _vehicles.AsReadOnly();
    public int SaveChangesCount { get; private set; }

    public Task AddAsync(Vehicle vehicle, CancellationToken ct)
    {
        _vehicles.Add(vehicle);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken ct)
    {
        SaveChangesCount++;
        return Task.CompletedTask;
    }

    public Task<Vehicle?> FindByIdAsync(Guid id, CancellationToken ct) =>
        Task.FromResult(_vehicles.FirstOrDefault(v => v.Id == id));

    public Task<bool> PlateNumberExistsAsync(string plateNumber, Guid? excludeId, CancellationToken ct)
    {
        var normalized = plateNumber.Trim();
        var exists = _vehicles.Any(v =>
            v.DeletedAt is null &&
            v.PlateNumber == normalized &&
            (!excludeId.HasValue || v.Id != excludeId.Value));

        return Task.FromResult(exists);
    }

    public Task<(IReadOnlyList<Vehicle> Items, string? NextCursor)> GetPageAsync(
        string? cursor,
        int pageSize,
        bool? isActive,
        CancellationToken ct)
    {
        var query = _vehicles.AsEnumerable();
        if (isActive.HasValue)
            query = query.Where(v => isActive.Value ? v.DeletedAt is null : v.DeletedAt is not null);

        return Task.FromResult<(IReadOnlyList<Vehicle>, string?)>(
            (query.Take(pageSize).ToList().AsReadOnly(), null));
    }
}
