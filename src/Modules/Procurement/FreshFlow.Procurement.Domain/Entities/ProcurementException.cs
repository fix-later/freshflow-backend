using FreshFlow.Procurement.Domain.Enums;
using FreshFlow.SharedKernel.Domain;

namespace FreshFlow.Procurement.Domain.Entities;

public sealed class ProcurementException : BaseEntity
{
    private ProcurementException() { }

    internal ProcurementException(
        Guid procurementBatchId,
        Guid marketProductId,
        ProcurementExceptionType type,
        int reportedQuantity,
        string? note,
        string? proofImageUrl,
        Guid reportedByUserId,
        DateTime reportedAt)
    {
        ProcurementBatchId = procurementBatchId;
        MarketProductId = marketProductId;
        Type = type;
        ReportedQuantity = reportedQuantity;
        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        ProofImageUrl = string.IsNullOrWhiteSpace(proofImageUrl) ? null : proofImageUrl.Trim();
        ReportedByUserId = reportedByUserId;
        ReportedAt = reportedAt;
        CreatedAt = reportedAt;
        UpdatedAt = reportedAt;
    }

    public Guid ProcurementBatchId { get; private set; }
    public Guid MarketProductId { get; private set; }
    public ProcurementExceptionType Type { get; private set; }
    public int ReportedQuantity { get; private set; }
    public string? Note { get; private set; }
    public string? ProofImageUrl { get; private set; }
    public Guid ReportedByUserId { get; private set; }
    public DateTime ReportedAt { get; private set; }
}
