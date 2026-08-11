using System.Xml.Linq;
using FluentAssertions;
using FreshFlow.Invoicing.Application.Abstractions;
using FreshFlow.Invoicing.Application.Queries.ExportInvoice;
using FreshFlow.Invoicing.Domain.Entities;
using NSubstitute;

namespace FreshFlow.Invoicing.UnitTests.Queries;

[Trait("Category", "Unit")]
public sealed class ExportInvoiceQueryHandlerTests
{
    private readonly IInvoiceRepository _repo = Substitute.For<IInvoiceRepository>();
    private readonly IRestaurantReader _restaurantReader = Substitute.For<IRestaurantReader>();

    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid RestaurantId = Guid.NewGuid();
    private static readonly Guid OtherRestaurantId = Guid.NewGuid();

    [Fact]
    public async Task IssuedInvoice_ReturnsStructuredXmlAsync()
    {
        var invoice = InvoiceFor(RestaurantId);
        invoice.MarkIssued(
            "K24TFF", "0001", "MCQT-1", "https://lookup", null, null, "stub",
            new DateTime(2026, 8, 4, 3, 2, 1, DateTimeKind.Utc));
        _repo.FindByIdAsync(invoice.Id, Arg.Any<CancellationToken>()).Returns(invoice);
        var handler = new ExportInvoiceQueryHandler(_repo, _restaurantReader);

        var result = await handler.Handle(new ExportInvoiceQuery(UserId, true, invoice.Id), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.FileName.Should().Be("invoice-dev-draft-K24TFF-0001.xml");
        var document = XDocument.Parse(result.Value.Xml);
        document.Root!.Name.LocalName.Should().Be("EInvoice");
        document.Root.Attribute("environment")!.Value.Should().Be("development");
        document.Root.Attribute("legalValue")!.Value.Should().Be("false");
        document.Root.Element("Notice")!.Value.Should().Be("BẢN NHÁP - KHÔNG CÓ GIÁ TRỊ THUẾ");
        document.Root.Element("Header")!.Element("TaxAuthorityCode")!.Value.Should().Be("MCQT-1");
        document.Root.Element("Header")!.Element("IssueDate")!.Value.Should().Be("2026-08-04T03:02:01Z");
        document.Root.Element("Seller")!.Element("TaxCode")!.Value.Should().Be("SELLER_TAX_CODE_PENDING");
        document.Root.Element("Buyer")!.Element("TaxCode")!.Value.Should().Be("0312345678");
        var line = document.Root.Element("Lines")!.Elements("Line").Should().ContainSingle().Subject;
        line.Element("ProductName")!.Value.Should().Be("A");
        line.Element("Unit")!.Value.Should().Be("kg");
        line.Element("Quantity")!.Value.Should().Be("1");
        line.Element("UnitPrice")!.Value.Should().Be("1000");
        line.Element("VatRateCode")!.Value.Should().Be("KCT");
        line.Element("VatRatePercent")!.Value.Should().Be("0");
        line.Element("LineSubtotal")!.Value.Should().Be("1000");
        line.Element("LineVatAmount")!.Value.Should().Be("0");
        line.Element("LineTotal")!.Value.Should().Be("1000");
        document.Root.Element("Totals")!.Element("Total")!.Value.Should().Be("1000");
    }

    [Fact]
    public async Task NonStubInvoice_DoesNotAddDevelopmentMarkersAsync()
    {
        var invoice = InvoiceFor(RestaurantId);
        invoice.MarkIssued(
            "K24TFF", "0002", "MCQT-2", "https://lookup", null, null, "misa", DateTime.UtcNow);
        _repo.FindByIdAsync(invoice.Id, Arg.Any<CancellationToken>()).Returns(invoice);
        var handler = new ExportInvoiceQueryHandler(_repo, _restaurantReader);

        var result = await handler.Handle(new ExportInvoiceQuery(UserId, true, invoice.Id), default);

        result.Value.FileName.Should().Be("invoice-K24TFF-0002.xml");
        var root = XDocument.Parse(result.Value.Xml).Root!;
        root.Attribute("environment").Should().BeNull();
        root.Attribute("legalValue").Should().BeNull();
        root.Element("Notice").Should().BeNull();
    }

    [Fact]
    public async Task NotIssued_ReturnsValidationErrorAsync()
    {
        var invoice = InvoiceFor(RestaurantId);
        _repo.FindByIdAsync(invoice.Id, Arg.Any<CancellationToken>()).Returns(invoice);
        var handler = new ExportInvoiceQueryHandler(_repo, _restaurantReader);

        var result = await handler.Handle(new ExportInvoiceQuery(UserId, true, invoice.Id), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("INVOICE_NOT_ISSUED");
    }

    [Fact]
    public async Task NonAdminOtherRestaurant_ReturnsNotFoundAsync()
    {
        var invoice = InvoiceFor(OtherRestaurantId);
        invoice.MarkIssued("K24TFF", "0001", "MCQT-1", null, null, null, "stub", DateTime.UtcNow);
        _repo.FindByIdAsync(invoice.Id, Arg.Any<CancellationToken>()).Returns(invoice);
        _restaurantReader.FindRestaurantIdByUserIdAsync(UserId, Arg.Any<CancellationToken>()).Returns(RestaurantId);
        var handler = new ExportInvoiceQueryHandler(_repo, _restaurantReader);

        var result = await handler.Handle(new ExportInvoiceQuery(UserId, false, invoice.Id), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("INVOICE_NOT_FOUND");
    }

    private static Invoice InvoiceFor(Guid restaurantId) =>
        new(Guid.NewGuid(), restaurantId, "0312345678", "Cty A", "123 Nguyễn Huệ", null,
            [new InvoiceLine("A", "kg", 1m, 1000m, "KCT", 0m)]);
}
