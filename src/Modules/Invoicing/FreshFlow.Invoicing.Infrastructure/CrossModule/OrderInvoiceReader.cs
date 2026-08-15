using FreshFlow.Infrastructure.Persistence;
using FreshFlow.Invoicing.Application.Abstractions;
using FreshFlow.Invoicing.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FreshFlow.Invoicing.Infrastructure.CrossModule;

internal sealed class OrderInvoiceReader(AppDbContext db) : IOrderInvoiceReader
{
    public async Task<OrderInvoiceSnapshot?> GetByOrderIdAsync(Guid orderId, CancellationToken ct)
    {
        var rows = await db.Set<OrderInvoiceRow>()
            .AsNoTracking()
            .Where(r => r.OrderId == orderId)
            .ToListAsync(ct);

        if (rows.Count == 0)
            return null;

        var lines = rows
            .Select(r => new OrderInvoiceLineSnapshot(
                r.ProductName, r.Unit, r.Quantity, r.UnitPrice, r.VatRateCode))
            .ToList();

        return new OrderInvoiceSnapshot(orderId, rows[0].RestaurantId, rows[0].DeliveryFee, lines);
    }

    public async Task<IReadOnlyList<Guid>> GetUninvoicedDeliveredOrderIdsAsync(
        int batchSize, CancellationToken ct)
    {
        if (batchSize <= 0)
            throw new ArgumentException("batchSize must be greater than zero.", nameof(batchSize));

        return await db.Set<OrderInvoiceRow>()
            .AsNoTracking()
            .Where(row => !db.Set<Invoice>().Any(invoice => invoice.OrderId == row.OrderId))
            .Select(row => row.OrderId)
            .Distinct()
            .Take(batchSize)
            .ToListAsync(ct);
    }
}
