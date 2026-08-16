using FreshFlow.Logistics.Domain.Entities;
using FreshFlow.Logistics.Domain.Enums;

namespace FreshFlow.Logistics.Application.Abstractions;

public interface IDeliveryRouteRepository
{
    public Task<DeliveryRoute?> FindByIdAsync(Guid id, CancellationToken ct);

    public Task<IReadOnlyList<DeliveryRoute>> GetByDriverAndDateAsync(
        Guid driverUserId,
        DateOnly serviceDate,
        CancellationToken ct);

    public Task<(IReadOnlyList<DeliveryRoute> Items, string? NextCursor)> GetPageAsync(
        string? cursor,
        int pageSize,
        DateOnly? serviceDate,
        RouteStatus? status,
        CancellationToken ct,
        Guid? hubId = null);

    public Task AddAsync(DeliveryRoute route, CancellationToken ct);

    public Task SaveChangesAsync(CancellationToken ct);

    public Task<bool> SaveAssignmentAsync(CancellationToken ct);

    public Task<bool> ExistsOtherRouteForVehicleOnDateAsync(
        Guid vehicleId,
        DateOnly serviceDate,
        Guid excludeRouteId,
        CancellationToken ct);

    public Task<IReadOnlySet<Guid>> GetReservedVehicleIdsAsync(
        DateOnly serviceDate,
        CancellationToken ct);
}
