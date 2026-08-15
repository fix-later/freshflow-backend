using FluentAssertions;
using FreshFlow.Invoicing.Application.Dtos;
using FreshFlow.Invoicing.Domain.Entities;
using FreshFlow.Invoicing.Infrastructure.Documents;
using QuestPDF.Infrastructure;

namespace FreshFlow.Invoicing.UnitTests.Documents;

[Trait("Category", "Unit")]
public sealed class InvoicePdfRendererTests
{
    static InvoicePdfRendererTests() => QuestPDF.Settings.License = LicenseType.Community;

    [Fact]
    public void Render_IssuedSandboxInvoice_ReturnsPdfBytes()
    {
        var invoice = new Invoice(
            Guid.NewGuid(), Guid.NewGuid(), "0312345678", "Công ty A", "123 Nguyễn Huệ", "a@example.com",
            [new InvoiceLine("Rau cải", "kg", 2m, 20_000m, "5", 5m)]);
        invoice.MarkIssued(
            "K24TFF", "0001", "DEV-MCQT-0001", null, null, null, "stub",
            new DateTime(2026, 8, 15, 3, 0, 0, DateTimeKind.Utc));

        var bytes = new InvoicePdfRenderer().Render(InvoiceDto.From(invoice));

        bytes.Should().StartWith("%PDF"u8.ToArray());
    }
}
