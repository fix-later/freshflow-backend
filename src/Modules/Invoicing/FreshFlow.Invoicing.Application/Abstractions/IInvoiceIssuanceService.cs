namespace FreshFlow.Invoicing.Application.Abstractions;

public interface IInvoiceIssuanceService
{
    /// <summary>Creates and issues an invoice for a just-delivered order. Idempotent per order.</summary>
    public Task IssueForDeliveredOrderAsync(Guid orderId, CancellationToken ct);

    /// <summary>Re-attempts issuance for still-pending invoices, honoring the attempt cap and backoff.</summary>
    public Task<int> RetryDueAsync(int maxAttempts, TimeSpan backoff, int batchSize, CancellationToken ct);
}
