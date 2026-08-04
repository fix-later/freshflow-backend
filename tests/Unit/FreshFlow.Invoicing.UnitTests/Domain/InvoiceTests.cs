using FluentAssertions;
using FreshFlow.Invoicing.Domain.Entities;
using FreshFlow.Invoicing.Domain.Enums;

namespace FreshFlow.Invoicing.UnitTests.Domain;

[Trait("Category", "Unit")]
public sealed class InvoiceTests
{
    [Fact]
    public void Line_ComputesSubtotalVatAndTotal()
    {
        var line = new InvoiceLine("Cà chua", "kg", 10m, 5000m, "5", 5m);

        line.LineSubtotal.Should().Be(50000m);
        line.LineVatAmount.Should().Be(2500m);
        line.LineTotal.Should().Be(52500m);
    }

    [Fact]
    public void Line_SubtotalMidpoint_RoundsAwayFromZero()
    {
        var line = new InvoiceLine("A", "kg", 0.01m, 0.50m, "KCT", 0m);

        line.LineSubtotal.Should().Be(0.01m);
    }

    [Fact]
    public void Line_VatMidpoint_RoundsAwayFromZero()
    {
        var line = new InvoiceLine("A", "kg", 1m, 0.05m, "10", 10m);

        line.LineVatAmount.Should().Be(0.01m);
        line.LineTotal.Should().Be(0.06m);
    }

    [Fact]
    public void Line_Kct_HasZeroVat()
    {
        var line = new InvoiceLine("Rau muống", "kg", 3m, 10000m, "KCT", 0m);

        line.LineVatAmount.Should().Be(0m);
        line.LineTotal.Should().Be(30000m);
    }

    [Fact]
    public void Invoice_AggregatesLineTotals()
    {
        var invoice = new Invoice(
            Guid.NewGuid(), Guid.NewGuid(), "0312345678", "Cty A", null, null,
            [
                new InvoiceLine("A", "kg", 10m, 5000m, "5", 5m),
                new InvoiceLine("B", "box", 2m, 10000m, "KCT", 0m)
            ]);

        invoice.SubTotal.Should().Be(70000m);
        invoice.VatAmount.Should().Be(2500m);
        invoice.Total.Should().Be(72500m);
        invoice.Status.Should().Be(InvoiceStatus.Draft);
    }

    [Fact]
    public void Invoice_RequiresAtLeastOneLine()
    {
        var act = () => new Invoice(Guid.NewGuid(), Guid.NewGuid(), "x", "y", null, null, []);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void MarkIssued_SetsTerminalStateAndCode()
    {
        var invoice = NewInvoice();

        invoice.MarkIssued("K24TFF", "0001", "MCQT-ABC", "http://x", null, null, "stub", DateTime.UtcNow);

        invoice.Status.Should().Be(InvoiceStatus.Issued);
        invoice.TaxAuthorityCode.Should().Be("MCQT-ABC");
        invoice.ProviderName.Should().Be("stub");
        invoice.ErrorReason.Should().BeNull();
    }

    [Fact]
    public void MarkIssued_RequiresTaxAuthorityCode()
    {
        var invoice = NewInvoice();

        var act = () => invoice.MarkIssued("s", "n", "  ", null, null, null, "stub", DateTime.UtcNow);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void UpdateBuyer_OnDraftAndPending_UpdatesBuyer()
    {
        var draft = NewInvoice();
        var pending = NewInvoice();
        pending.MarkAwaitingBuyerInfo("missing");

        draft.UpdateBuyer("0101", "Draft Co", "Draft Addr", "draft@x.vn");
        pending.UpdateBuyer("0202", "Pending Co", "Pending Addr", "pending@x.vn");

        draft.BuyerTaxCode.Should().Be("0101");
        draft.BuyerLegalName.Should().Be("Draft Co");
        draft.BuyerAddress.Should().Be("Draft Addr");
        draft.BuyerEmail.Should().Be("draft@x.vn");
        pending.BuyerTaxCode.Should().Be("0202");
        pending.Status.Should().Be(InvoiceStatus.PendingIssuance);
    }

    [Fact]
    public void UpdateBuyer_OnIssued_Throws()
    {
        var invoice = NewInvoice();
        invoice.MarkIssued("s", "n", "code", null, null, null, "stub", DateTime.UtcNow);

        var act = () => invoice.UpdateBuyer("0101", "Co", "Addr", null);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void MarkIssuanceFailed_IncrementsRetryAndStaysPending()
    {
        var invoice = NewInvoice();

        invoice.MarkIssuanceFailed("boom");

        invoice.Status.Should().Be(InvoiceStatus.PendingIssuance);
        invoice.RetryCount.Should().Be(1);
        invoice.ErrorReason.Should().Be("boom");
    }

    [Fact]
    public void MarkAwaitingBuyerInfo_StaysPendingWithoutIncrementingRetry()
    {
        var invoice = NewInvoice();

        invoice.MarkAwaitingBuyerInfo("MISSING_BUYER_TAX_PROFILE");

        invoice.Status.Should().Be(InvoiceStatus.PendingIssuance);
        invoice.RetryCount.Should().Be(0);
        invoice.ErrorReason.Should().Be("MISSING_BUYER_TAX_PROFILE");
    }

    [Fact]
    public void MarkFailed_IsTerminal()
    {
        var invoice = NewInvoice();

        invoice.MarkFailed("dead");

        invoice.Status.Should().Be(InvoiceStatus.Failed);
        invoice.ErrorReason.Should().Be("dead");
    }

    private static Invoice NewInvoice() =>
        new(Guid.NewGuid(), Guid.NewGuid(), "0312345678", "Cty A", null, null,
            [new InvoiceLine("A", "kg", 1m, 1000m, "KCT", 0m)]);
}
