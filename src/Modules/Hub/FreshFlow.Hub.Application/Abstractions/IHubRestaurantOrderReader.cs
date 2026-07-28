namespace FreshFlow.Hub.Application.Abstractions;

public interface IHubRestaurantOrderReader
{
    public Task<IReadOnlyList<HubRestaurantOrder>> ListByHubAndServiceDateAsync(
        Guid hubId,
        IReadOnlyCollection<string> statuses,
        DateOnly serviceDate,
        CancellationToken ct);
}

public sealed record HubRestaurantOrder(
    Guid OrderId,
    Guid RestaurantId,
    string RestaurantName);
