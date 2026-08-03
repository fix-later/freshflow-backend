using System.Xml;
using System.Xml.Linq;
using FreshFlow.Invoicing.Application.Abstractions;
using FreshFlow.Invoicing.Domain.Entities;
using FreshFlow.Invoicing.Domain.Enums;
using FreshFlow.Invoicing.Domain.Validation;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Invoicing.Application.Queries.ExportInvoice;

public sealed record ExportInvoiceQuery(Guid UserId, bool IsAdmin, Guid InvoiceId)
    : IQuery<ExportInvoiceResult>;

public sealed record ExportInvoiceResult(string Xml, string FileName);

internal sealed class ExportInvoiceQueryHandler(
    IInvoiceRepository invoices,
    IRestaurantReader restaurantReader)
    : IRequestHandler<ExportInvoiceQuery, Result<ExportInvoiceResult>>
{
    public async Task<Result<ExportInvoiceResult>> Handle(ExportInvoiceQuery request, CancellationToken ct)
    {
        var invoice = await invoices.FindByIdAsync(request.InvoiceId, ct);
        if (invoice is null)
            return Result<ExportInvoiceResult>.Failure(Error.NotFound("INVOICE", request.InvoiceId));

        if (!request.IsAdmin)
        {
            var owned = await restaurantReader.FindRestaurantIdByUserIdAsync(request.UserId, ct);
            if (owned is null || owned != invoice.RestaurantId)
                return Result<ExportInvoiceResult>.Failure(Error.NotFound("INVOICE", request.InvoiceId));
        }

        if (invoice.Status != InvoiceStatus.Issued)
            return Result<ExportInvoiceResult>.Failure(Error.Validation(
                "INVOICE_NOT_ISSUED", "Only issued invoices can be exported."));

        if (!IsComplete(invoice))
            return Result<ExportInvoiceResult>.Failure(Error.Validation(
                "INVOICE_EXPORT_INCOMPLETE", "The persisted invoice is missing required export data."));

        var document = BuildDocument(invoice);
        return Result<ExportInvoiceResult>.Success(new ExportInvoiceResult(
            document.ToString(SaveOptions.DisableFormatting),
            $"invoice-{invoice.Serial}-{invoice.Number}.xml"));
    }

    private static bool IsComplete(Invoice invoice) =>
        !string.IsNullOrWhiteSpace(invoice.Serial) &&
        !string.IsNullOrWhiteSpace(invoice.Number) &&
        !string.IsNullOrWhiteSpace(invoice.TaxAuthorityCode) &&
        invoice.IssuedAt is not null &&
        InvoiceBuyerValidator.GetErrorCode(
            invoice.BuyerTaxCode, invoice.BuyerLegalName, invoice.BuyerAddress) is null &&
        invoice.Lines.Count > 0 &&
        invoice.Lines.All(line => !string.IsNullOrWhiteSpace(line.Unit));

    private static XDocument BuildDocument(Invoice invoice) => new(
        new XDeclaration("1.0", "utf-8", null),
        new XElement("EInvoice",
            new XAttribute("version", "1.0"),
            new XElement("Header",
                Element("InvoiceId", invoice.Id),
                Element("OrderId", invoice.OrderId),
                Element("Serial", invoice.Serial!),
                Element("Number", invoice.Number!),
                Element("TaxAuthorityCode", invoice.TaxAuthorityCode!),
                Element("IssueDate", XmlConvert.ToString(
                    invoice.IssuedAt!.Value, XmlDateTimeSerializationMode.Utc))),
            new XElement("Seller",
                Element("TaxCode", "SELLER_TAX_CODE_PENDING"),
                Element("LegalName", "FreshFlow Seller (configuration pending)"),
                Element("Address", "SELLER_ADDRESS_PENDING")),
            new XElement("Buyer",
                Element("TaxCode", invoice.BuyerTaxCode),
                Element("LegalName", invoice.BuyerLegalName),
                Element("Address", invoice.BuyerAddress!),
                Element("Email", invoice.BuyerEmail ?? string.Empty)),
            new XElement("Lines", invoice.Lines
                .OrderBy(line => line.Id)
                .Select((line, index) => LineElement(line, index + 1))),
            new XElement("Totals",
                Element("Subtotal", invoice.SubTotal),
                Element("VatAmount", invoice.VatAmount),
                Element("Total", invoice.Total))));

    private static XElement LineElement(InvoiceLine line, int position) =>
        new("Line",
            new XAttribute("position", position),
            Element("ProductName", line.ProductName),
            Element("Unit", line.Unit!),
            Element("Quantity", line.Quantity),
            Element("UnitPrice", line.UnitPrice),
            Element("VatRateCode", line.VatRateCode),
            Element("VatRatePercent", line.VatRatePercent),
            Element("LineSubtotal", line.LineSubtotal),
            Element("LineVatAmount", line.LineVatAmount),
            Element("LineTotal", line.LineTotal));

    private static XElement Element(string name, object value) =>
        new(name, value is decimal amount ? XmlConvert.ToString(amount) : value);
}
