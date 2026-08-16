namespace FreshFlow.Analytics.Infrastructure.CrossModule;

internal sealed class ProcurementBatchRow
{
    public Guid BatchId { get; init; }
    public DateOnly BatchDate { get; init; }
    public Guid MarketId { get; init; }
    public string Status { get; init; } = string.Empty;
    public DateTime? ManifestedAt { get; init; }
    public DateTime? HandedOffAt { get; init; }
}

