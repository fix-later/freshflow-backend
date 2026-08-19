using System.Globalization;
using FreshFlow.Invoicing.Application.Common;
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

    /// <summary>Rows of the "Tổng hợp" block, in the fixed order mandated by the invoice form.</summary>
    private static readonly (string Label, string RateText, Func<InvoiceLineDto, bool> Match)[] SummaryRows =
    [
        ("Hàng hóa không kê khai nộp thuế:", VatRateResolver.CodeKkknt, l => Code(l) == VatRateResolver.CodeKkknt),
        ("Hàng hóa không chịu thuế suất GTGT:", VatRateResolver.CodeKct, l => Code(l) == VatRateResolver.CodeKct),
        ("Hàng hóa chịu thuế suất GTGT:", "0%", l => IsRate(l, 0m)),
        ("Hàng hóa chịu thuế suất GTGT:", "5%", l => IsRate(l, 5m)),
        ("Hàng hóa chịu thuế suất GTGT:", "8%", l => IsRate(l, 8m)),
        ("Hàng hóa chịu thuế suất GTGT:", "10%", l => IsRate(l, 10m))
    ];

    public byte[] Render(InvoiceDto invoice) => Document.Create(container =>
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(1.2f, Unit.Centimetre);
            page.DefaultTextStyle(style => style.FontSize(8));

            page.Header().Column(column =>
            {
                column.Item().Background(Colors.Red.Lighten4).Padding(6).AlignCenter()
                    .Text("BẢN NHÁP / DEMO - KHÔNG CÓ GIÁ TRỊ THUẾ")
                    .FontColor(Colors.Red.Darken3).Bold();
                column.Item().PaddingTop(10).AlignCenter().Text("HÓA ĐƠN GIÁ TRỊ GIA TĂNG")
                    .FontSize(16).Bold();
                column.Item().AlignCenter().Text("(VAT INVOICE)").FontSize(9).Italic();
            });

            page.Content().PaddingTop(10).Column(column =>
            {
                column.Spacing(8);
                column.Item().Row(row =>
                {
                    row.RelativeItem().Column(left =>
                    {
                        left.Item().Text("Đơn vị bán hàng (Seller): FreshFlow").Bold();
                        left.Item().Text("Mã số thuế (Tax code): Chưa cấu hình");
                        left.Item().Text("Địa chỉ (Address): Chưa cấu hình");
                    });
                    row.RelativeItem().AlignRight().Column(right =>
                    {
                        right.Item().Text($"Ký hiệu (Serial): {invoice.Serial}");
                        right.Item().Text($"Số (No.): {invoice.Number}").Bold();
                        right.Item().Text($"Ngày lập (Date): {IssueDate(invoice):dd/MM/yyyy HH:mm}");
                    });
                });

                column.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
                column.Item().Column(buyer =>
                {
                    buyer.Item().Text($"Đơn vị mua hàng (Buyer): {invoice.BuyerLegalName}").Bold();
                    buyer.Item().Text($"Mã số thuế (Tax code): {invoice.BuyerTaxCode}");
                    buyer.Item().Text($"Địa chỉ (Address): {invoice.BuyerAddress}");
                    if (!string.IsNullOrWhiteSpace(invoice.BuyerEmail))
                        buyer.Item().Text($"Email nhận hóa đơn: {invoice.BuyerEmail}");
                });

                var charges = invoice.Lines.Where(IsCharge).ToList();
                var goods = invoice.Lines.Where(line => !IsCharge(line)).ToList();

                column.Item().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.ConstantColumn(24);   // 1  Stt
                        columns.RelativeColumn(4.2f); // 2  Tên hàng hóa, dịch vụ
                        columns.RelativeColumn(0.9f); // 3  Đvt
                        columns.RelativeColumn(1.0f); // 4  Số lượng
                        columns.RelativeColumn(1.3f); // 5  Đơn giá
                        columns.RelativeColumn(1.5f); // 6  Thành tiền trước thuế
                        columns.RelativeColumn(1.1f); // 7  Thuế suất GTGT
                        columns.RelativeColumn(1.3f); // 8  Tiền thuế GTGT
                        columns.RelativeColumn(1.6f); // 9  Thành tiền sau thuế
                    });

                    table.Header(header =>
                    {
                        HeaderLabel(header, "Stt", "(No.)");
                        HeaderLabel(header, "Tên hàng hóa, dịch vụ", "(Item)");
                        HeaderLabel(header, "Đvt", "(UOM)");
                        HeaderLabel(header, "Số lượng", "(Quantity)");
                        HeaderLabel(header, "Đơn giá", "(Price)");
                        HeaderLabel(header, "Thành tiền trước thuế", "(Amount excluding VAT)");
                        HeaderLabel(header, "Thuế suất GTGT", "(VAT Rate)");
                        HeaderLabel(header, "Tiền thuế GTGT", "(VAT)");
                        HeaderLabel(header, "Thành tiền sau thuế", "(Total Amount)");

                        foreach (var legend in new[]
                                 { "1", "2", "3", "4", "5", "6 = 4 x 5", "7", "8 = 6 x 7", "9 = 6 + 8" })
                            header.Cell().Element(HeaderCell).AlignCenter().Text(legend).FontSize(7);
                    });

                    var ordinal = 0;
                    foreach (var line in goods)
                        LineRow(table, line, ++ordinal);

                    if (charges.Count > 0)
                    {
                        SectionRow(table, "Chi phí khác (Other charges)");
                        foreach (var line in charges)
                            LineRow(table, line, ++ordinal);
                    }
                });

                column.Item().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(3.5f);
                        columns.RelativeColumn(1.4f);
                        columns.RelativeColumn(1.6f);
                        columns.RelativeColumn(1.4f);
                        columns.RelativeColumn(1.7f);
                    });

                    table.Header(header =>
                    {
                        HeaderLabel(header, "Tổng hợp", "(In summary)");
                        HeaderLabel(header, "Thuế suất thuế GTGT", "(VAT Rate)");
                        HeaderLabel(header, "Thành tiền trước thuế", "(Amount excluding VAT)");
                        HeaderLabel(header, "Tiền thuế GTGT", "(VAT Amount)");
                        HeaderLabel(header, "Thành tiền sau thuế", "(Total Amount)");
                    });

                    foreach (var summary in SummaryRows)
                    {
                        var matched = invoice.Lines.Where(summary.Match).ToList();
                        var isTaxable = summary.RateText.EndsWith('%');
                        Cell(table, summary.Label);
                        Cell(table, summary.RateText, Align.Center, bold: true);
                        Cell(table, Money(matched.Sum(l => l.LineSubtotal)), Align.Right);
                        Cell(table, isTaxable ? Money(matched.Sum(l => l.LineVatAmount)) : @"\", Align.Right);
                        Cell(table, Money(matched.Sum(l => l.LineTotal)), Align.Right);
                    }

                    Cell(table, "Tổng cộng tiền thanh toán (Total of payment):", Align.Left, bold: true);
                    Cell(table, string.Empty);
                    Cell(table, Money(invoice.SubTotal), Align.Right, bold: true);
                    Cell(table, Money(invoice.VatAmount), Align.Right, bold: true);
                    Cell(table, Money(invoice.Total), Align.Right, bold: true);
                });

                column.Item().Row(row =>
                {
                    row.ConstantItem(110).Column(label =>
                    {
                        label.Item().Text("Số tiền viết bằng chữ:").Bold();
                        label.Item().Text("Amount (in words)").Italic();
                    });
                    row.RelativeItem().Text(VietnameseAmountInWords.Convert(invoice.Total)).Italic();
                });

                column.Item().Background(Colors.Grey.Lighten3).Padding(6)
                    .Text($"Mã CQT mô phỏng: {invoice.TaxAuthorityCode}\nMã đơn hàng: {invoice.OrderId}")
                    .FontSize(7).FontColor(Colors.Grey.Darken2);
            });

            page.Footer().Row(row =>
            {
                row.RelativeItem().Text("Tài liệu thử nghiệm được tạo tự động bởi FreshFlow.")
                    .FontSize(7).FontColor(Colors.Grey.Darken1);
                row.AutoItem().Text(text =>
                {
                    text.CurrentPageNumber();
                    text.Span(" / ");
                    text.TotalPages();
                });
            });
        });
    }).GeneratePdf();

    private enum Align { Left, Center, Right }

    private static IContainer HeaderCell(IContainer container) => container
        .Background(Colors.Green.Lighten4).BorderBottom(0.5f).BorderColor(Colors.Grey.Medium).Padding(3);

    private static void HeaderLabel(TableCellDescriptor header, string vietnamese, string english) =>
        header.Cell().Element(HeaderCell).AlignCenter().Column(cell =>
        {
            cell.Item().AlignCenter().Text(vietnamese).Bold();
            cell.Item().AlignCenter().Text(english).FontSize(7).Italic();
        });

    private static void Cell(TableDescriptor table, string text, Align align = Align.Left, bool bold = false)
    {
        var cell = table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(3);
        var content = align switch
        {
            Align.Right => cell.AlignRight(),
            Align.Center => cell.AlignCenter(),
            _ => cell
        };
        var span = content.Text(text);
        if (bold)
            span.Bold();
    }

    private static void LineRow(TableDescriptor table, InvoiceLineDto line, int ordinal)
    {
        Cell(table, ordinal.ToString(Vietnamese), Align.Center);
        Cell(table, line.ProductName);
        Cell(table, line.Unit ?? string.Empty, Align.Center);
        Cell(table, Number(line.Quantity), Align.Right);
        Cell(table, Money(line.UnitPrice), Align.Right);
        Cell(table, Money(line.LineSubtotal), Align.Right);
        Cell(table, RateText(line), Align.Center);
        Cell(table, VatAmountText(line), Align.Right);
        Cell(table, Money(line.LineTotal), Align.Right);
    }

    /// <summary>Full-width band that separates the goods rows from the service charges.</summary>
    private static void SectionRow(TableDescriptor table, string label) =>
        table.Cell().ColumnSpan(9).Background(Colors.Grey.Lighten3)
            .BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(3)
            .Text(label).Bold();

    /// <summary>Non-goods lines appended at issuance (delivery fee); grouped apart in the PDF.</summary>
    private static bool IsCharge(InvoiceLineDto line) => line.ProductName == InvoiceLineNames.DeliveryFee;

    private static DateTime IssueDate(InvoiceDto invoice) => TimeZoneInfo.ConvertTimeFromUtc(
        DateTime.SpecifyKind(invoice.IssuedAt ?? invoice.CreatedAt, DateTimeKind.Utc), VietnamTimeZone);

    private static string Code(InvoiceLineDto line) => VatRateResolver.Normalize(line.VatRateCode);

    private static bool IsRate(InvoiceLineDto line, decimal percent) =>
        Code(line) is not (VatRateResolver.CodeKct or VatRateResolver.CodeKkknt)
        && line.VatRatePercent == percent;

    /// <summary>KKKNT / KCT print their code; taxable lines print the percent.</summary>
    private static string RateText(InvoiceLineDto line) =>
        Code(line) is VatRateResolver.CodeKct or VatRateResolver.CodeKkknt
            ? Code(line)
            : $"{Number(line.VatRatePercent)}%";

    /// <summary>Non-taxable lines show "\" rather than a zero, per the VN invoice form.</summary>
    private static string VatAmountText(InvoiceLineDto line) =>
        Code(line) is VatRateResolver.CodeKct or VatRateResolver.CodeKkknt
            ? @"\"
            : Money(line.LineVatAmount);

    private static string Number(decimal value) => value.ToString("0.##", Vietnamese);

    private static string Money(decimal value) => value.ToString("N0", Vietnamese);
}
