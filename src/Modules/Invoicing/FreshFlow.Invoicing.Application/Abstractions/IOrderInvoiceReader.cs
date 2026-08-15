namespace FreshFlow.Invoicing.Application.Abstractions;

/// <summary>
/// Cross-module read of a delivered order's confirmed commercial terms. Quantity, unit price,
/// VAT rate, and delivery fee are the immutable confirmation snapshots; procurement actuals are
/// internal costs and never change the buyer's invoice. Implemented in
/// Infrastructure via a keyless ToSqlQuery seam — Invoicing has no project reference to
/// Orders/Catalog.
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
