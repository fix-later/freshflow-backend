using System.Globalization;
using FreshFlow.Orders.Application.Abstractions;
using FreshFlow.Orders.Application.Dtos;
using FreshFlow.Orders.Application.Services;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace FreshFlow.Orders.Infrastructure.Documents;

/// <summary>
/// Renders a <see cref="CreditStatementDto"/> to a printable A4 PDF using QuestPDF. Pure
/// layout over the existing DTO — no new fields, no persistence. Timestamps are stored UTC
/// and converted to Asia/Ho_Chi_Minh here, at the display boundary (mirrors the conversion in
/// <see cref="CreditStatementPeriodCalculator"/> and in
/// CreditStatementGeneratedIntegrationEventHandler.BuildBody). Money is formatted N0/invariant
/// with a " đ" suffix, matching the notification body.
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

                page.Header().Column(column =>
                {
                    column.Item().Text("SAO KÊ CÔNG NỢ").FontSize(16).Bold();
                    column.Item().Text(
                        $"Kỳ: {ToLocal(statement.PeriodStart):dd/MM/yyyy} - {ToLocal(statement.PeriodEnd):dd/MM/yyyy}");
                    column.Item().Text($"Ngày xuất: {ToLocal(statement.GeneratedAt):dd/MM/yyyy HH:mm}");
                    column.Item().Text($"Hạn thanh toán: {ToLocal(statement.DueDate):dd/MM/yyyy}");
                });

                page.Content().Column(column =>
                {
                    column.Spacing(6);
                    column.Item().Text($"Dư nợ đầu kỳ: {Money(statement.OpeningBalance)}");
                    column.Item().Text($"Tổng phát sinh nợ: {Money(statement.TotalCharges)}");
                    column.Item().Text($"Tổng thanh toán: {Money(statement.TotalSettlements)}");
                    column.Item().Text($"Tổng hoàn tiền: {Money(statement.TotalRefunds)}");
                    column.Item().Text($"Dư nợ cuối kỳ: {Money(statement.ClosingBalance)}").Bold();

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
                            header.Cell().Text("Thời gian").Bold();
                            header.Cell().Text("Loại").Bold();
                            header.Cell().Text("Số tiền").Bold();
                            header.Cell().Text("Dư nợ sau GD").Bold();
                            header.Cell().Text("Ghi chú").Bold();
                        });

                        foreach (var line in statement.Lines)
                        {
                            table.Cell().Text(ToLocal(line.OccurredAt).ToString("dd/MM/yyyy HH:mm"));
                            table.Cell().Text(line.Type);
                            table.Cell().Text(Money(line.Amount));
                            table.Cell().Text(Money(line.BalanceAfter));
                            table.Cell().Text(line.Note ?? line.Reference ?? "-");
                        }
                    });
                });

                page.Footer().AlignCenter().Text(x =>
                {
                    x.CurrentPageNumber();
                    x.Span(" / ");
                    x.TotalPages();
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
}
