using System.Globalization;
using System.Text;
using FreshFlow.Analytics.Application.Dtos;
using FreshFlow.Analytics.Application.Queries.GetDeliveryPerformance;
using FreshFlow.Analytics.Application.Queries.GetOrderMetrics;
using FreshFlow.Analytics.Application.Queries.GetPriceTrends;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Analytics.Application.Queries.ExportAnalytics;

internal sealed class ExportAnalyticsQueryHandler(ISender sender)
    : IRequestHandler<ExportAnalyticsQuery, Result<CsvExportDto>>
{
    private const int MaxRows = 50_000;
    private const string ContentType = "text/csv";
    private const string PriceHeader =
        "marketProductId,productName,marketName,interval,timestamp,avgPrice,minPrice,maxPrice,snapshotCount";
    private const string OrderHeader = "date,orderCount,revenueVND";
    private const string DeliveryHeader =
        "totalDeliveries,onTimeCount,lateCount,onTimeRatePercent,failedCount," +
        "avgDeliveryDurationMinutes,durationSampleCount," +
        "avgVehicleUtilizationPercent,utilizationSampleCount";

    public async Task<Result<CsvExportDto>> Handle(ExportAnalyticsQuery request, CancellationToken ct) =>
        request.Dataset.ToLowerInvariant() switch
        {
            "price-history" => await ExportPriceHistoryAsync(request, ct),
            "order-history" => await ExportOrderHistoryAsync(request, ct),
            "delivery-performance" => await ExportDeliveryPerformanceAsync(request, ct),
            _ => Result<CsvExportDto>.Failure(Error.Validation(
                "VALIDATION_ERROR",
                "Unsupported export dataset.")),
        };

    private async Task<Result<CsvExportDto>> ExportPriceHistoryAsync(
        ExportAnalyticsQuery request,
        CancellationToken ct)
    {
        var result = await sender.Send(
            new GetPriceTrendsQuery(request.MarketProductIds, request.From, request.To, null),
            ct);
        if (result.IsFailure)
            return Result<CsvExportDto>.Failure(result.Error);

        var rowCount = result.Value.Series.Sum(series => series.Points.Count);
        if (rowCount > MaxRows)
            return TooLarge();

        var csv = StartCsv(PriceHeader);
        foreach (var series in result.Value.Series)
        {
            foreach (var point in series.Points)
            {
                AppendRow(csv,
                    series.MarketProductId.ToString(),
                    series.ProductName,
                    series.MarketName,
                    series.Interval,
                    point.Timestamp.ToString("O", CultureInfo.InvariantCulture),
                    Format(point.AvgPrice),
                    Format(point.MinPrice),
                    Format(point.MaxPrice),
                    point.SnapshotCount.ToString(CultureInfo.InvariantCulture));
            }
        }

        return CreateResult(request, "price-history", csv);
    }

    private async Task<Result<CsvExportDto>> ExportOrderHistoryAsync(
        ExportAnalyticsQuery request,
        CancellationToken ct)
    {
        var result = await sender.Send(
            new GetOrderMetricsQuery(request.From, request.To, null, null),
            ct);
        if (result.IsFailure)
            return Result<CsvExportDto>.Failure(result.Error);
        if (result.Value.Buckets.Count > MaxRows)
            return TooLarge();

        var csv = StartCsv(OrderHeader);
        foreach (var bucket in result.Value.Buckets)
        {
            AppendRow(csv,
                bucket.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                bucket.OrderCount.ToString(CultureInfo.InvariantCulture),
                Format(bucket.RevenueVND));
        }

        return CreateResult(request, "order-history", csv);
    }

    private async Task<Result<CsvExportDto>> ExportDeliveryPerformanceAsync(
        ExportAnalyticsQuery request,
        CancellationToken ct)
    {
        var result = await sender.Send(
            new GetDeliveryPerformanceQuery(request.From, request.To),
            ct);
        if (result.IsFailure)
            return Result<CsvExportDto>.Failure(result.Error);

        var csv = StartCsv(DeliveryHeader);
        var metrics = result.Value;
        if (metrics.TotalDeliveries != 0 || metrics.OnTimeCount != 0 || metrics.LateCount != 0 ||
            metrics.FailedCount != 0 || metrics.DurationSampleCount != 0 || metrics.UtilizationSampleCount != 0)
        {
            AppendRow(csv,
                metrics.TotalDeliveries.ToString(CultureInfo.InvariantCulture),
                metrics.OnTimeCount.ToString(CultureInfo.InvariantCulture),
                metrics.LateCount.ToString(CultureInfo.InvariantCulture),
                Format(metrics.OnTimeRatePercent),
                metrics.FailedCount.ToString(CultureInfo.InvariantCulture),
                Format(metrics.AvgDeliveryDurationMinutes),
                metrics.DurationSampleCount.ToString(CultureInfo.InvariantCulture),
                Format(metrics.AvgVehicleUtilizationPercent),
                metrics.UtilizationSampleCount.ToString(CultureInfo.InvariantCulture));
        }

        return CreateResult(request, "delivery-performance", csv);
    }

    private static StringBuilder StartCsv(string header) =>
        new StringBuilder(header).Append("\r\n");

    private static void AppendRow(StringBuilder csv, params string?[] fields)
    {
        for (var index = 0; index < fields.Length; index++)
        {
            if (index > 0)
                csv.Append(',');
            csv.Append(EscapeCsv(fields[index] ?? string.Empty));
        }
        csv.Append("\r\n");
    }

    private static string EscapeCsv(string value) =>
        value.Contains(',') || value.Contains('"') || value.Contains('\r') || value.Contains('\n')
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;

    private static string Format(decimal value) => value.ToString(CultureInfo.InvariantCulture);

    private static string Format(decimal? value) =>
        value?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;

    private static Result<CsvExportDto> CreateResult(
        ExportAnalyticsQuery request,
        string dataset,
        StringBuilder csv)
    {
        var fileName = $"analytics-{dataset}-" +
            $"{request.From.ToString("yyyyMMdd", CultureInfo.InvariantCulture)}-" +
            $"{request.To.ToString("yyyyMMdd", CultureInfo.InvariantCulture)}.csv";
        // BOM: Excel decodes a BOM-less UTF-8 CSV as ANSI, mangling Vietnamese product names.
        var preamble = Encoding.UTF8.GetPreamble();
        var content = Encoding.UTF8.GetBytes(csv.ToString());
        var bytes = new byte[preamble.Length + content.Length];
        preamble.CopyTo(bytes, 0);
        content.CopyTo(bytes, preamble.Length);

        return Result<CsvExportDto>.Success(new CsvExportDto(fileName, ContentType, bytes));
    }

    private static Result<CsvExportDto> TooLarge() =>
        Result<CsvExportDto>.Failure(Error.Validation(
            "VALIDATION_ERROR",
            "Export exceeds 50,000 rows; narrow your from/to range."));
}
