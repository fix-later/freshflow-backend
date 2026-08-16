using System.Globalization;
using FreshFlow.Orders.Application.Abstractions;
using FreshFlow.Orders.Application.Dtos;
using FreshFlow.Orders.Application.Services;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace FreshFlow.Orders.Infrastructure.Documents;

/// <summary>
/// Renders a <see cref="CreditStatementDto"/> to a printable A4 PDF using QuestPDF.
/// </summary>
public sealed class StatementPdfRenderer : IStatementPdfRenderer
{
    public byte[] Render(CreditStatementDto statement)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Background(Colors.Green.Lighten5).Padding(8).Column(column =>
                {
                    column.Item().Text("FRESHFLOW").FontSize(11).FontColor(Colors.Green.Darken2).Bold();
                    column.Item().PaddingTop(4).Text("SAO KÊ CÔNG NỢ").FontSize(18).Bold();
                    column.Item().Text($"Mã sao kê: {statement.Id}").FontSize(8).FontColor(Colors.Grey.Darken1);
                    column.Item().Text($"Nhà hàng: {statement.RestaurantId}").FontSize(8).FontColor(Colors.Grey.Darken1);
                    column.Item().Text(
                        $"Kỳ: {ToLocal(statement.PeriodStart):dd/MM/yyyy} - {ToLocal(statement.PeriodEnd).AddDays(-1):dd/MM/yyyy}");
                    column.Item().Text($"Ngày xuất: {ToLocal(statement.GeneratedAt):dd/MM/yyyy HH:mm}");
                    column.Item().Text($"Hạn thanh toán: {ToLocal(statement.DueDate):dd/MM/yyyy}");
                });

                page.Content().Column(column =>
                {
                    column.Spacing(6);
                    column.Item().PaddingTop(10).Background(Colors.Grey.Lighten3).Padding(8).Column(summary =>
                    {
                        summary.Item().Row(row =>
                        {
                            row.RelativeItem().Text($"Dư nợ đầu kỳ: {Money(statement.OpeningBalance)}");
                            row.RelativeItem().Text($"Phát sinh nợ: {Money(statement.TotalCharges)}");
                        });
                        summary.Item().Row(row =>
                        {
                            row.RelativeItem().Text($"Thanh toán: {Money(statement.TotalSettlements)}");
                            row.RelativeItem().Text($"Hoàn tiền: {Money(statement.TotalRefunds)}");
                        });
                        summary.Item().PaddingTop(2).Text($"Dư nợ cuối kỳ: {Money(statement.ClosingBalance)}").Bold();
                    });

                    if (statement.Lines.Count == 0)
                    {
                        column.Item().PaddingTop(10).Text("Không có giao dịch trong kỳ.");
                        return;
                    }

                    column.Item().PaddingTop(10).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(3);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Background(Colors.Green.Lighten4).Padding(4).Text("Thời gian").Bold();
                            header.Cell().Background(Colors.Green.Lighten4).Padding(4).Text("Loại").Bold();
                            header.Cell().Background(Colors.Green.Lighten4).Padding(4).AlignRight().Text("Số tiền").Bold();
                            header.Cell().Background(Colors.Green.Lighten4).Padding(4).AlignRight().Text("Dư nợ sau GD").Bold();
                            header.Cell().Background(Colors.Green.Lighten4).Padding(4).Text("Chi tiết").Bold();
                        });

                        foreach (var line in statement.Lines)
                        {
                            table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4)
                                .Text(ToLocal(line.OccurredAt).ToString("dd/MM/yyyy HH:mm"));
                            table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4)
                                .Text(TransactionType(line.Type));
                            table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).AlignRight()
                                .Text(Money(line.Amount));
                            table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).AlignRight()
                                .Text(Money(line.BalanceAfter));
                            table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4)
                                .Text(Details(line));
                        }
                    });
                });

                page.Footer().Row(row =>
                {
                    row.RelativeItem().Text("Tài liệu được tạo tự động bởi FreshFlow.").FontSize(8)
                        .FontColor(Colors.Grey.Darken1);
                    row.AutoItem().Text(x =>
                    {
                        x.CurrentPageNumber();
                        x.Span(" / ");
                        x.TotalPages();
                    });
                });
            });
        });

        return document.GeneratePdf();
    }

    private static DateTime ToLocal(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.SpecifyKind(utc, DateTimeKind.Utc),
            CreditStatementPeriodCalculator.VietnamTimeZone);

    private static string Money(decimal amount) => amount.ToString("N0", CultureInfo.InvariantCulture) + " đ";

    private static string TransactionType(string type) => type switch
    {
        "charge" => "Phát sinh nợ",
        "settlement" => "Thanh toán",
        "refund" => "Hoàn tiền",
        "adjustment" => "Điều chỉnh",
        _ => type
    };

    private static string Details(CreditStatementLineDto line)
    {
        var parts = new List<string>(4);
        if (line.OrderId is not null)
            parts.Add($"Đơn hàng: {line.OrderId}");
        if (line.PaymentMethod is not null)
            parts.Add($"Hình thức: {PaymentMethod(line.PaymentMethod)}");
        if (!string.IsNullOrWhiteSpace(line.Reference))
            parts.Add($"Tham chiếu: {line.Reference}");
        if (!string.IsNullOrWhiteSpace(line.Note))
            parts.Add(line.Note);
        return parts.Count == 0 ? "-" : string.Join("\n", parts);
    }

    private static string PaymentMethod(string method) => method switch
    {
        "bank_transfer" => "Chuyển khoản",
        "manual" => "Thủ công",
        _ => method
    };
}
