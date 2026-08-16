using FreshFlow.Invoicing.Domain.Enums;
using FreshFlow.SharedKernel.Domain;

namespace FreshFlow.Invoicing.Domain.Entities;

/// <summary>
/// A VAT e-invoice for one delivered order. Created in <see cref="InvoiceStatus.Draft"/> from the
/// delivered-order snapshot, then transitioned to <see cref="InvoiceStatus.Issued"/> once the
/// e-invoice provider (NCC) returns a tax-authority code (mã CQT). Lines are frozen at creation
/// and the monetary totals are computed once from those lines. The row is mutable only through
/// the explicit issuance transitions below — it is never edited field-by-field.
/// </summary>
public sealed class Invoice : BaseEntity
{
    private const int MaxErrorReasonLength = 500;
    private readonly List<InvoiceLine> _lines = [];

    private Invoice() { } // EF Core

    public Invoice(
        Guid orderId,
        Guid restaurantId,
        string buyerTaxCode,
        string buyerLegalName,
        string? buyerAddress,
        string? buyerEmail,
        IReadOnlyList<InvoiceLine> lines)
    {
        if (orderId == Guid.Empty)
            throw new ArgumentException("Order id is required.", nameof(orderId));
        if (restaurantId == Guid.Empty)
            throw new ArgumentException("Restaurant id is required.", nameof(restaurantId));
        if (lines.Count == 0)
            throw new ArgumentException("An invoice must have at least one line.", nameof(lines));

        OrderId = orderId;
        RestaurantId = restaurantId;
        BuyerTaxCode = buyerTaxCode;
        BuyerLegalName = buyerLegalName;
        BuyerAddress = buyerAddress;
        BuyerEmail = buyerEmail;
        Status = InvoiceStatus.Draft;
        _lines.AddRange(lines);

        SubTotal = _lines.Sum(l => l.LineSubtotal);
        VatAmount = _lines.Sum(l => l.LineVatAmount);
        Total = _lines.Sum(l => l.LineTotal);
    }

    public Guid OrderId { get; private set; }
    public Guid RestaurantId { get; private set; }
    public string BuyerTaxCode { get; private set; } = string.Empty;
    public string BuyerLegalName { get; private set; } = string.Empty;
    public string? BuyerAddress { get; private set; }
    public string? BuyerEmail { get; private set; }

    public InvoiceStatus Status { get; private set; }
    public string? Serial { get; private set; }
    public string? Number { get; private set; }
    public string? TaxAuthorityCode { get; private set; }
    public string? LookupUrl { get; private set; }
    public string? PdfRef { get; private set; }
    public string? XmlRef { get; private set; }
    public string? ProviderName { get; private set; }
    public DateTime? IssuedAt { get; private set; }
    public string? ErrorReason { get; private set; }
    public int RetryCount { get; private set; }

    public decimal SubTotal { get; private set; }
    public decimal VatAmount { get; private set; }
    public decimal Total { get; private set; }

    public IReadOnlyCollection<InvoiceLine> Lines => _lines.AsReadOnly();

    public void UpdateBuyer(string taxCode, string legalName, string? address, string? email)
    {
        if (Status is not InvoiceStatus.Draft and not InvoiceStatus.PendingIssuance)
            throw new InvalidOperationException("Buyer information can only be updated before issuance.");

        BuyerTaxCode = taxCode;
        BuyerLegalName = legalName;
        BuyerAddress = address;
        BuyerEmail = email;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>Records a successful issuance (provider returned the mã CQT). Terminal state.</summary>
    public void MarkIssued(
        string serial,
        string number,
        string taxAuthorityCode,
        string? lookupUrl,
        string? xmlRef,
        string? pdfRef,
        string providerName,
        DateTime issuedAt)
    {
        if (string.IsNullOrWhiteSpace(taxAuthorityCode))
            throw new ArgumentException(
                "Tax authority code is required to issue an invoice.", nameof(taxAuthorityCode));

        Serial = serial;
        Number = number;
        TaxAuthorityCode = taxAuthorityCode;
        LookupUrl = lookupUrl;
        XmlRef = xmlRef;
        PdfRef = pdfRef;
        ProviderName = providerName;
        IssuedAt = issuedAt;
        ErrorReason = null;
        Status = InvoiceStatus.Issued;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Records a retryable failure and leaves the invoice in <see cref="InvoiceStatus.PendingIssuance"/>
    /// so the retry job picks it up again while it stays under the attempt cap.
    /// </summary>
    public void MarkIssuanceFailed(string reason)
    {
        ErrorReason = TruncateErrorReason(reason);
        RetryCount++;
        Status = InvoiceStatus.PendingIssuance;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkAwaitingBuyerInfo(string reason)
    {
        ErrorReason = TruncateErrorReason(reason);
        Status = InvoiceStatus.PendingIssuance;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>Gives up after the retry cap is exhausted; the invoice needs manual intervention.</summary>
    public void MarkFailed(string reason)
    {
        ErrorReason = TruncateErrorReason(reason);
        Status = InvoiceStatus.Failed;
        UpdatedAt = DateTime.UtcNow;
    }

    private static string TruncateErrorReason(string reason) =>
        reason.Length <= MaxErrorReasonLength ? reason : reason[..MaxErrorReasonLength];
}
