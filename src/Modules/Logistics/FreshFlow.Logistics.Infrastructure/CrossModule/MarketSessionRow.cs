namespace FreshFlow.Logistics.Infrastructure.CrossModule;

internal sealed class MarketSessionRow
{
    public Guid Id { get; init; }
    public Guid HubId { get; init; }
    public DateOnly ServiceDate { get; init; }
    public string Status { get; init; } = string.Empty;
}
