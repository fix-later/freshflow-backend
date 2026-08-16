namespace FreshFlow.Analytics.Infrastructure.CrossModule;

internal sealed class ProcurementExceptionRow
{
    public Guid ProcurementBatchId { get; init; }
    public string Type { get; init; } = string.Empty;
}
