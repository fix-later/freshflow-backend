namespace FreshFlow.Analytics.Infrastructure.CrossModule;

internal sealed class ProcurementBatchRow
{
    public Guid BatchId { get; init; }
    public DateOnly BatchDate { get; init; }
    public string Status { get; init; } = string.Empty;
}

