namespace FreshFlow.Analytics.Infrastructure.CrossModule;

internal sealed class OrderSummaryRow
{
    public Guid OrderId { get; init; }
    public string Status { get; init; } = string.Empty;
    public decimal TotalAmount { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? CancelledAt { get; init; }
}

