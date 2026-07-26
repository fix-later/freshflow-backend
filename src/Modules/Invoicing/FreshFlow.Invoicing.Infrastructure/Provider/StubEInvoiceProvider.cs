using FreshFlow.Invoicing.Application.Abstractions;
using FreshFlow.SharedKernel.Application;
using Microsoft.Extensions.Logging;

namespace FreshFlow.Invoicing.Infrastructure.Provider;

/// <summary>
/// Development/sandbox stand-in for a real e-invoice provider (NCC). Returns a deterministic fake
/// tax-authority code (mã CQT) so the whole issuance flow runs and is testable without a provider
/// contract. Swap for a real MISA/VNPT/Viettel implementation behind IEInvoiceProvider at go-live —
/// config section Invoicing:EInvoice:* is reserved for it.
/// </summary>
internal sealed class StubEInvoiceProvider(
    TimeProvider clock,
    ILogger<StubEInvoiceProvider> logger) : IEInvoiceProvider
{
    public string Name => "stub";

    public Task<Result<IssuedInvoice>> IssueAsync(InvoiceIssueRequest request, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var shortId = request.InvoiceId.ToString("N")[..8].ToUpperInvariant();

        var issued = new IssuedInvoice(
            Serial: "K24TFF",
            Number: shortId,
            TaxAuthorityCode: $"MCQT-{shortId}",
            LookupUrl: $"https://sandbox.invoice.local/lookup/{shortId}",
            XmlRef: $"stub://xml/{request.InvoiceId}",
            PdfRef: $"stub://pdf/{request.InvoiceId}",
            IssuedAt: now);

        logger.LogInformation(
            "Stub e-invoice issued for InvoiceId={InvoiceId} (fake mã CQT {Code}).",
            request.InvoiceId, issued.TaxAuthorityCode);

        return Task.FromResult(Result<IssuedInvoice>.Success(issued));
    }
}
