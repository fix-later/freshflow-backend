namespace FreshFlow.Analytics.Infrastructure.CrossModule;

internal sealed class ProcurementBatchItemRow
{
    public Guid ProcurementBatchId { get; init; }
    public decimal? ReferenceUnitPrice { get; init; }
    public int? ActualQuantity { get; init; }
    public decimal? ActualUnitPrice { get; init; }
}
