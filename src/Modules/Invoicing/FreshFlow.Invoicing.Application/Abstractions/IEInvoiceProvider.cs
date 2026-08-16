using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Invoicing.Application.Abstractions;

/// <summary>
/// Adapter over a licensed e-invoice provider (NCC). Hides provider lock-in: the dev stub returns a
/// fake mã CQT so the full flow runs without a real provider; a MISA/VNPT/Viettel implementation
/// drops in behind the same interface, selected by config. Adjustment/cancellation (hóa đơn điều
/// chỉnh) is a v2 concern and is intentionally not declared here yet (YAGNI).
/// </summary>
public interface IEInvoiceProvider
{
    /// <summary>Provider identifier recorded on the issued invoice (e.g. "stub", "misa").</summary>
    public string Name { get; }

    /// <summary>
    /// Sends invoice data to the NCC to be digitally signed and assigned a tax-authority code.
    /// The provider MUST treat <see cref="InvoiceIssueRequest.InvoiceId"/> as an idempotency key:
    /// repeated calls with the same InvoiceId return the same external invoice and never issue a duplicate.
    /// </summary>
    // ponytail: Cross-replica atomic claim is deferred while deployment remains single-instance.
    public Task<Result<IssuedInvoice>> IssueAsync(InvoiceIssueRequest request, CancellationToken ct);
}

public sealed record InvoiceIssueRequest(
    Guid InvoiceId,
    Guid OrderId,
    string BuyerTaxCode,
    string BuyerLegalName,
    string? BuyerAddress,
    string? BuyerEmail,
    decimal SubTotal,
    decimal VatAmount,
    decimal Total,
    IReadOnlyList<InvoiceIssueLine> Lines);

public sealed record InvoiceIssueLine(
    string ProductName,
    string Unit,
    decimal Quantity,
    decimal UnitPrice,
    string VatRateCode,
    decimal VatRatePercent,
    decimal LineSubtotal,
    decimal LineVatAmount,
    decimal LineTotal);

public sealed record IssuedInvoice(
    string Serial,
    string Number,
    string TaxAuthorityCode,
    string? LookupUrl,
    string? XmlRef,
    string? PdfRef,
    DateTime IssuedAt);
