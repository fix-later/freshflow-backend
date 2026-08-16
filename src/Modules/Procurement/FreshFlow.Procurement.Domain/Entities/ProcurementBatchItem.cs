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
    public int? ActualQuantity { get; private set; }
    public decimal? ActualUnitPrice { get; private set; }
    public DateTime? PurchasedAt { get; private set; }
    public Guid? AssignedAgentUserId { get; private set; }
    public DateTime? AssignedAt { get; private set; }

    internal void AddQuantity(int extraQuantity) => TotalQuantity += extraQuantity;

    internal void SetReferencePrice(decimal price) => ReferenceUnitPrice = price;

    internal void AssignTo(Guid agentUserId, DateTime assignedAt)
    {
        AssignedAgentUserId = agentUserId;
        AssignedAt = assignedAt;
        UpdatedAt = assignedAt;
    }

    internal void Unassign()
    {
        AssignedAgentUserId = null;
        AssignedAt = null;
    }

    internal void ConfirmPurchase(int actualQuantity, decimal actualUnitPrice, DateTime purchasedAt)
    {
        ActualQuantity = actualQuantity;
        ActualUnitPrice = actualUnitPrice;
        PurchasedAt = purchasedAt;
        UpdatedAt = purchasedAt;
    }

    internal void ClearPurchase(DateTime capturedAt)
    {
        ActualQuantity = null;
        ActualUnitPrice = null;
        PurchasedAt = null;
        UpdatedAt = capturedAt;
    }
}
