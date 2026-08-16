using FreshFlow.Hub.Application.Abstractions;

namespace FreshFlow.Hub.UnitTests.TestDoubles;

internal sealed class InMemoryHubRestaurantOrderReader : IHubRestaurantOrderReader
{
    private readonly List<(Guid HubId, DateOnly ServiceDate, HubRestaurantOrder Order)> _orders = [];

    public void Add(Guid hubId, DateOnly serviceDate, Guid orderId) =>
        _orders.Add((
            hubId,
            serviceDate,
            new HubRestaurantOrder(orderId, Guid.NewGuid(), "Restaurant")));

    public Task<IReadOnlyList<HubRestaurantOrder>> ListByHubAndServiceDateAsync(
        Guid hubId,
        IReadOnlyCollection<string> statuses,
        DateOnly serviceDate,
        CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<HubRestaurantOrder>>(
            _orders
                .Where(item => item.HubId == hubId && item.ServiceDate == serviceDate)
                .Select(item => item.Order)
                .ToList());
}
