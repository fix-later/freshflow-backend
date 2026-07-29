using FreshFlow.Logistics.Application.Abstractions;
using FreshFlow.Logistics.Domain.Entities;
using FreshFlow.Logistics.Domain.Enums;

namespace FreshFlow.Logistics.UnitTests.TestDoubles;

internal sealed class InMemoryDeliveryRouteRepository : IDeliveryRouteRepository
{
    private readonly List<DeliveryRoute> _routes = [];

    public IReadOnlyList<DeliveryRoute> Routes => _routes.AsReadOnly();
    public int SaveChangesCount { get; private set; }
    public int SaveAssignmentCount { get; private set; }
    public int GetByDriverAndDateCount { get; private set; }
    public bool SaveAssignmentResult { get; set; } = true;

    public Task<DeliveryRoute?> FindByIdAsync(Guid id, CancellationToken ct) =>
        Task.FromResult(_routes.FirstOrDefault(route => route.Id == id));

    public Task<IReadOnlyList<DeliveryRoute>> GetByDriverAndDateAsync(
        Guid driverUserId,
        DateOnly serviceDate,
        CancellationToken ct)
    {
        GetByDriverAndDateCount++;
        return Task.FromResult<IReadOnlyList<DeliveryRoute>>(
            _routes
                .Where(route =>
                    route.DriverUserId == driverUserId &&
                    route.ServiceDate == serviceDate &&
                    route.DeletedAt == null)
                .OrderBy(route => route.CreatedAt)
                .ToList()
                .AsReadOnly());
    }

    public Task<bool> ExistsOtherRouteForVehicleOnDateAsync(
        Guid vehicleId,
        DateOnly serviceDate,
        Guid excludeRouteId,
        CancellationToken ct) =>
        Task.FromResult(_routes.Any(route =>
            route.Id != excludeRouteId &&
            route.VehicleId == vehicleId &&
            route.ServiceDate == serviceDate &&
            route.Status != RouteStatus.cancelled &&
            route.DeletedAt == null));

    public Task<(IReadOnlyList<DeliveryRoute> Items, string? NextCursor)> GetPageAsync(
        string? cursor,
        int pageSize,
        DateOnly? serviceDate,
        RouteStatus? status,
        CancellationToken ct,
        Guid? hubId = null)
    {
        var query = _routes.AsEnumerable();

        if (serviceDate.HasValue)
            query = query.Where(route => route.ServiceDate == serviceDate.Value);

        if (status.HasValue)
            query = query.Where(route => route.Status == status.Value);

        if (hubId.HasValue)
            query = query.Where(route => route.HubId == hubId.Value);

        return Task.FromResult<(IReadOnlyList<DeliveryRoute>, string?)>(
            (query.OrderByDescending(route => route.CreatedAt).ToList().AsReadOnly(), null));
    }

    public Task AddAsync(DeliveryRoute route, CancellationToken ct)
    {
        _routes.Add(route);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken ct)
    {
        SaveChangesCount++;
        return Task.CompletedTask;
    }

    public Task<bool> SaveAssignmentAsync(CancellationToken ct)
    {
        SaveAssignmentCount++;
        return Task.FromResult(SaveAssignmentResult);
    }
}
