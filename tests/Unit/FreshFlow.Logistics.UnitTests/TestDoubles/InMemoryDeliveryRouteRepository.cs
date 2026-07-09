using FreshFlow.Logistics.Application.Abstractions;
using FreshFlow.Logistics.Domain.Entities;
using FreshFlow.Logistics.Domain.Enums;

namespace FreshFlow.Logistics.UnitTests.TestDoubles;

internal sealed class InMemoryDeliveryRouteRepository : IDeliveryRouteRepository
{
    private readonly List<DeliveryRoute> _routes = [];

    public IReadOnlyList<DeliveryRoute> Routes => _routes.AsReadOnly();
    public int SaveChangesCount { get; private set; }

    public Task<DeliveryRoute?> FindByIdAsync(Guid id, CancellationToken ct) =>
        Task.FromResult(_routes.FirstOrDefault(route => route.Id == id));

    public Task<(IReadOnlyList<DeliveryRoute> Items, string? NextCursor)> GetPageAsync(
        string? cursor,
        int pageSize,
        DateOnly? serviceDate,
        RouteStatus? status,
        CancellationToken ct)
    {
        var query = _routes.AsEnumerable();

        if (serviceDate.HasValue)
            query = query.Where(route => route.ServiceDate == serviceDate.Value);

        if (status.HasValue)
            query = query.Where(route => route.Status == status.Value);

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
}
