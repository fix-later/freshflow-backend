using FreshFlow.Hub.Application.Abstractions;

namespace FreshFlow.Hub.UnitTests.TestDoubles;

internal sealed class InMemoryDeliveryRouteReader : IDeliveryRouteReader
{
    private readonly Dictionary<Guid, DeliveryRouteLookupDto> _routes = [];

    public void Add(
        Guid routeId,
        Guid? driverUserId = null,
        string status = "planned",
        Guid? hubId = null) =>
        _routes[routeId] = new DeliveryRouteLookupDto(routeId, hubId, status, driverUserId);

    public Task<DeliveryRouteLookupDto?> FindByIdAsync(Guid routeId, CancellationToken ct) =>
        Task.FromResult(_routes.GetValueOrDefault(routeId));
}
