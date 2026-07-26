namespace FreshFlow.Invoicing.Domain.Entities;

/// <summary>
/// A frozen line captured onto an <see cref="Invoice"/> at issuance time. Never updated after
/// creation — the invoice it belongs to is a legal snapshot. Monetary fields are computed once
/// in the constructor from quantity, unit price and the resolved VAT rate.
/// </summary>
public sealed class InvoiceLine
{
    private InvoiceLine() { } // EF Core

    public InvoiceLine(
        string productName,
        decimal quantity,
        decimal unitPrice,
        string vatRateCode,
        decimal vatRatePercent)
    {
        if (string.IsNullOrWhiteSpace(productName))
            throw new ArgumentException("Product name is required.", nameof(productName));
        if (quantity < 0m)
            throw new ArgumentOutOfRangeException(nameof(quantity), quantity, "Quantity must be non-negative.");
        if (unitPrice < 0m)
            throw new ArgumentOutOfRangeException(nameof(unitPrice), unitPrice, "Unit price must be non-negative.");
        if (vatRatePercent < 0m)
            throw new ArgumentOutOfRangeException(
                nameof(vatRatePercent), vatRatePercent, "VAT rate must be non-negative.");

        Id = Guid.NewGuid();
        ProductName = productName;
        Quantity = quantity;
        UnitPrice = unitPrice;
        VatRateCode = vatRateCode;
        VatRatePercent = vatRatePercent;
        // Exact VN half-up rounding policy is pending business sign-off.
        LineSubtotal = decimal.Round(quantity * unitPrice, 2, MidpointRounding.AwayFromZero);
        LineVatAmount = decimal.Round(
            LineSubtotal * vatRatePercent / 100m, 2, MidpointRounding.AwayFromZero);
        LineTotal = LineSubtotal + LineVatAmount;
    }

    public Guid Id { get; private set; }
    public Guid InvoiceId { get; private set; }
    public string ProductName { get; private set; } = string.Empty;
    public decimal Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }

    /// <summary>Raw rate code as stored on the product, e.g. "KCT", "0", "5", "8", "10".</summary>
    public string VatRateCode { get; private set; } = string.Empty;

    /// <summary>Numeric percent resolved from <see cref="VatRateCode"/>; KCT and "0" both resolve to 0.</summary>
    public decimal VatRatePercent { get; private set; }

    public decimal LineSubtotal { get; private set; }
    public decimal LineVatAmount { get; private set; }
    public decimal LineTotal { get; private set; }
}
