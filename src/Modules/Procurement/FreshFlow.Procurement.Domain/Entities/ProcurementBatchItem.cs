using FreshFlow.SharedKernel.Domain;

namespace FreshFlow.Procurement.Domain.Entities;

public sealed class ProcurementBatchItem : BaseEntity
{
    private ProcurementBatchItem() { }

    internal ProcurementBatchItem(
        Guid procurementBatchId,
        Guid marketProductId,
        string productNameSnapshot,
        int totalQuantity)
    {
        ProcurementBatchId = procurementBatchId;
        MarketProductId = marketProductId;
        ProductNameSnapshot = productNameSnapshot;
        TotalQuantity = totalQuantity;
    }

    public Guid ProcurementBatchId { get; private set; }
    public Guid MarketProductId { get; private set; }
    public string ProductNameSnapshot { get; private set; } = string.Empty;
    public int TotalQuantity { get; private set; }
    public decimal? ReferenceUnitPrice { get; private set; }

    internal void SetReferencePrice(decimal price) => ReferenceUnitPrice = price;
}
