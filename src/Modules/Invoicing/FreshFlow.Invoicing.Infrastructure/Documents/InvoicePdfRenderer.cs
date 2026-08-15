using System.Globalization;
using FreshFlow.Invoicing.Application.Dtos;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace FreshFlow.Invoicing.Infrastructure.Documents;

public sealed class InvoicePdfRenderer
{
    private static readonly CultureInfo Vietnamese = CultureInfo.GetCultureInfo("vi-VN");
    private static readonly TimeZoneInfo VietnamTimeZone =
        TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");

    public byte[] Render(InvoiceDto invoice) => Document.Create(container =>
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(1.5f, Unit.Centimetre);
            page.DefaultTextStyle(style => style.FontSize(9));

            page.Header().Column(column =>
            {
                column.Item().Background(Colors.Red.Lighten4).Padding(8).AlignCenter()
                    .Text("BẢN NHÁP / DEMO - KHÔNG CÓ GIÁ TRỊ THUẾ")
                    .FontColor(Colors.Red.Darken3).Bold();
                column.Item().PaddingTop(12).AlignCenter().Text("HÓA ĐƠN GIÁ TRỊ GIA TĂNG")
                    .FontSize(18).Bold();
                column.Item().AlignCenter().Text("FRESHFLOW - MÔI TRƯỜNG PHÁT TRIỂN")
                    .FontColor(Colors.Green.Darken2).Bold();
            });

            page.Content().PaddingTop(12).Column(column =>
            {
                column.Spacing(8);
                column.Item().Row(row =>
                {
                    row.RelativeItem().Column(left =>
                    {
                        left.Item().Text("Đơn vị bán hàng: FreshFlow").Bold();
                        left.Item().Text("Mã số thuế: Chưa cấu hình");
                        left.Item().Text("Địa chỉ: Chưa cấu hình");
                    });
                    row.RelativeItem().AlignRight().Column(right =>
                    {
                        right.Item().Text($"Ký hiệu: {invoice.Serial}");
                        right.Item().Text($"Số: {invoice.Number}").Bold();
                        right.Item().Text($"Ngày lập: {IssueDate(invoice):dd/MM/yyyy HH:mm}");
                    });
                });

                column.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
                column.Item().Column(buyer =>
                {
                    buyer.Item().Text($"Đơn vị mua hàng: {invoice.BuyerLegalName}").Bold();
                    buyer.Item().Text($"Mã số thuế: {invoice.BuyerTaxCode}");
                    buyer.Item().Text($"Địa chỉ: {invoice.BuyerAddress}");
                    if (!string.IsNullOrWhiteSpace(invoice.BuyerEmail))
                        buyer.Item().Text($"Email nhận hóa đơn: {invoice.BuyerEmail}");
                });

                column.Item().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.ConstantColumn(28);
                        columns.RelativeColumn(4);
                        columns.RelativeColumn(1.2f);
                        columns.RelativeColumn(1.2f);
                        columns.RelativeColumn(1.8f);
                        columns.RelativeColumn(1.2f);
                        columns.RelativeColumn(2);
                    });

                    table.Header(header =>
                    {
                        header.Cell().Element(HeaderCell).Text("STT").Bold();
                        header.Cell().Element(HeaderCell).Text("Tên hàng hóa, dịch vụ").Bold();
                        header.Cell().Element(HeaderCell).Text("ĐVT").Bold();
                        header.Cell().Element(HeaderCell).AlignRight().Text("Số lượng").Bold();
                        header.Cell().Element(HeaderCell).AlignRight().Text("Đơn giá").Bold();
                        header.Cell().Element(HeaderCell).AlignRight().Text("Thuế").Bold();
                        header.Cell().Element(HeaderCell).AlignRight().Text("Thành tiền").Bold();
                    });

                    foreach (var pair in invoice.Lines.Select((line, index) => (line, index)))
                    {
                        Cell(table, (pair.index + 1).ToString(Vietnamese));
                        Cell(table, pair.line.ProductName);
                        Cell(table, pair.line.Unit ?? string.Empty);
                        Cell(table, Number(pair.line.Quantity), true);
                        Cell(table, Money(pair.line.UnitPrice), true);
                        Cell(table, Vat(pair.line), true);
                        Cell(table, Money(pair.line.LineTotal), true);
                    }
                });

                column.Item().AlignRight().Width(260).Column(totals =>
                {
                    Total(totals, "Cộng tiền hàng:", invoice.SubTotal);
                    Total(totals, "Tiền thuế GTGT:", invoice.VatAmount);
                    totals.Item().PaddingTop(4).BorderTop(1).Row(row =>
                    {
                        row.RelativeItem().Text("Tổng cộng thanh toán:").Bold();
                        row.ConstantItem(110).AlignRight().Text(Money(invoice.Total)).FontSize(11).Bold();
                    });
                });

                column.Item().Background(Colors.Grey.Lighten3).Padding(6)
                    .Text($"Mã CQT mô phỏng: {invoice.TaxAuthorityCode}\nMã đơn hàng: {invoice.OrderId}")
                    .FontSize(8).FontColor(Colors.Grey.Darken2);
            });

            page.Footer().Row(row =>
            {
                row.RelativeItem().Text("Tài liệu thử nghiệm được tạo tự động bởi FreshFlow.")
                    .FontSize(8).FontColor(Colors.Grey.Darken1);
                row.AutoItem().Text(text =>
                {
                    text.CurrentPageNumber();
                    text.Span(" / ");
                    text.TotalPages();
                });
            });
        });
    }).GeneratePdf();

    private static IContainer HeaderCell(IContainer container) =>
        container.Background(Colors.Green.Lighten4).Padding(4);

    private static void Cell(TableDescriptor table, string text, bool right = false)
    {
        var cell = table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4);
        (right ? cell.AlignRight() : cell).Text(text);
    }

    private static void Total(ColumnDescriptor column, string label, decimal amount) =>
        column.Item().Row(row =>
        {
            row.RelativeItem().Text(label);
            row.ConstantItem(110).AlignRight().Text(Money(amount));
        });

    private static DateTime IssueDate(InvoiceDto invoice) => TimeZoneInfo.ConvertTimeFromUtc(
        DateTime.SpecifyKind(invoice.IssuedAt ?? invoice.CreatedAt, DateTimeKind.Utc), VietnamTimeZone);

    private static string Vat(InvoiceLineDto line) =>
        line.VatRateCode == "KCT" ? "KCT" : $"{Number(line.VatRatePercent)}%";

    private static string Number(decimal value) => value.ToString("0.##", Vietnamese);

    private static string Money(decimal value) => value.ToString("N0", Vietnamese) + " đ";
}
