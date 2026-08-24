namespace FreshFlow.Invoicing.Application.Abstractions;

/// <summary>
/// Cross-module read of a delivered order's billable terms. Quantity is the delivered quantity
/// (AUDIT-2026-08-23 C4 — falls back to the ordered quantity for a line procurement never
/// touched); unit price, VAT rate, and delivery fee remain the immutable confirmation snapshots.
/// Implemented in Infrastructure via a keyless ToSqlQuery seam — Invoicing has no project
/// reference to Orders/Catalog.
/// </summary>
public interface IOrderInvoiceReader
{
    public Task<OrderInvoiceSnapshot?> GetByOrderIdAsync(Guid orderId, CancellationToken ct);

    public Task<IReadOnlyList<Guid>> GetUninvoicedDeliveredOrderIdsAsync(
        int batchSize, CancellationToken ct);
}

public sealed record OrderInvoiceSnapshot(
    Guid OrderId,
    Guid RestaurantId,
    decimal DeliveryFee,
    IReadOnlyList<OrderInvoiceLineSnapshot> Lines);

public sealed record OrderInvoiceLineSnapshot(
    string ProductName,
    string? Unit,
    decimal Quantity,
    decimal UnitPrice,
    string? VatRateCode);
