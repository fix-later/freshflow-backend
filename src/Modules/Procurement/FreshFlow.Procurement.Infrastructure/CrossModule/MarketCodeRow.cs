namespace FreshFlow.Procurement.Infrastructure.CrossModule;

internal sealed class MarketCodeRow
{
    public Guid Id { get; init; }
    public string? Code { get; init; }
    public string Name { get; init; } = string.Empty;
}
