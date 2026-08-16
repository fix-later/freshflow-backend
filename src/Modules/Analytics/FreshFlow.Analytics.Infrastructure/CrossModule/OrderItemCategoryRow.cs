namespace FreshFlow.Analytics.Infrastructure.CrossModule;

internal sealed class OrderItemCategoryRow
{
    public Guid OrderId { get; init; }
    public int Quantity { get; init; }
    public string? CategoryName { get; init; }
}
