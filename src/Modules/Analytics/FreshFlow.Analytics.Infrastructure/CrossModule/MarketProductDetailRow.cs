namespace FreshFlow.Analytics.Infrastructure.CrossModule;

internal sealed class MarketProductDetailRow
{
    public Guid MarketProductId { get; init; }
    public string ProductName { get; init; } = string.Empty;
    public string MarketName { get; init; } = string.Empty;
}
