namespace FreshFlow.Invoicing.Domain.Enums;

/// <summary>
/// Lifecycle of a VAT e-invoice. Draft → PendingIssuance → Issued (terminal, carries the
/// tax-authority code) or Failed (retries exhausted). Adjusted/Cancelled are reserved for v2
/// (hóa đơn điều chỉnh/hủy) and are not produced by v1.
/// </summary>
public enum InvoiceStatus
{
    Draft = 0,
    PendingIssuance = 1,
    Issued = 2,
    Failed = 3,
    Adjusted = 4,
    Cancelled = 5
}
