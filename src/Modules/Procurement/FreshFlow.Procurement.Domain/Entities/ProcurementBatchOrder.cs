using FreshFlow.SharedKernel.Domain;

namespace FreshFlow.Procurement.Domain.Entities;

public sealed class ProcurementBatchOrder : BaseEntity
{
    private ProcurementBatchOrder() { }

    internal ProcurementBatchOrder(Guid procurementBatchId, Guid orderId)
    {
        ProcurementBatchId = procurementBatchId;
        OrderId = orderId;
    }

    public Guid ProcurementBatchId { get; private set; }
    public Guid OrderId { get; private set; }
}
