namespace FreshFlow.Invoicing.Application.Abstractions;

/// <summary>
/// Cross-module read of a delivered order's billable lines. Quantity is the delivered amount
/// (ActualQuantity when Hub recorded a shortage, otherwise the ordered Quantity); unit price is the
/// procurement actual price when available, otherwise the price locked at confirm; the VAT rate code
/// is the confirmation snapshot. Implemented in
/// Infrastructure via a keyless ToSqlQuery seam — Invoicing has no project reference to
/// Orders/Catalog.
/// </summary>
public interface IOrderInvoiceReader
{
    public Task<OrderInvoiceSnapshot?> GetByOrderIdAsync(Guid orderId, CancellationToken ct);
}

public sealed record OrderInvoiceSnapshot(
    Guid OrderId,
    Guid RestaurantId,
    IReadOnlyList<OrderInvoiceLineSnapshot> Lines);

public sealed record OrderInvoiceLineSnapshot(
    string ProductName,
    string? Unit,
    decimal Quantity,
    decimal UnitPrice,
    string? VatRateCode);
