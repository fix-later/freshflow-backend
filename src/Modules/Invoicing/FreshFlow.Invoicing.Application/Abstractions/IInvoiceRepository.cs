using FreshFlow.Invoicing.Domain.Entities;
using FreshFlow.Invoicing.Domain.Enums;

namespace FreshFlow.Invoicing.Application.Abstractions;

public interface IInvoiceRepository
{
    /// <summary>True if any invoice already exists for the order — issuance is idempotent per order.</summary>
    public Task<bool> ExistsForOrderAsync(Guid orderId, CancellationToken ct);

    public Task AddAsync(Invoice invoice, CancellationToken ct);

    /// <summary>Loads a single invoice with its lines.</summary>
    public Task<Invoice?> FindByIdAsync(Guid invoiceId, CancellationToken ct);

    /// <summary>
    /// Invoices still awaiting a tax-authority code (Draft or PendingIssuance), under the attempt
    /// cap and last touched before the backoff threshold. Includes lines so the retry can re-issue.
    /// </summary>
    public Task<IReadOnlyList<Invoice>> GetRetryablePageAsync(
        int maxAttempts, DateTime backoffThreshold, int batchSize, CancellationToken ct);

    public Task<(IReadOnlyList<Invoice> Items, int Total)> ListAsync(
        Guid? restaurantId, InvoiceStatus? status, int skip, int take, CancellationToken ct);

    public Task<InvoiceTotals> SummarizeIssuedAsync(
        Guid? restaurantId, DateTime fromUtc, DateTime toUtc, CancellationToken ct);

    public Task SaveChangesAsync(CancellationToken ct);
}

public sealed record InvoiceTotals(int Count, decimal SubTotal, decimal VatAmount, decimal Total);
