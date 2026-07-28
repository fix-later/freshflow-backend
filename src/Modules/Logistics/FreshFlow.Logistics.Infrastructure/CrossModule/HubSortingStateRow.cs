namespace FreshFlow.Logistics.Infrastructure.CrossModule;

internal sealed class HubSortingStateRow
{
    public Guid? RouteId { get; init; }
    public Guid HubId { get; init; }
    public Guid? MarketId { get; init; }
    public DateOnly ServiceDate { get; init; }
    public string Status { get; init; } = string.Empty;
}
