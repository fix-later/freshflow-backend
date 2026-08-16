using System.Collections;
using System.Globalization;
using System.Text;
using FluentAssertions;
using FluentValidation;
using FreshFlow.Analytics.Application.Dtos;
using FreshFlow.Analytics.Application.Queries.ExportAnalytics;
using FreshFlow.Analytics.Application.Queries.GetDeliveryPerformance;
using FreshFlow.Analytics.Application.Queries.GetOrderMetrics;
using FreshFlow.Analytics.Application.Queries.GetPriceTrends;
using FreshFlow.Analytics.Infrastructure;
using FreshFlow.SharedKernel.Application;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FreshFlow.Analytics.UnitTests.Queries;

[Trait("Category", "Unit")]
public sealed class ExportAnalyticsQueryHandlerTests
{
    private static readonly DateOnly From = new(2026, 7, 16);
    private static readonly DateOnly To = new(2026, 7, 17);
    private static readonly Guid MarketProductId = Guid.NewGuid();

    [Fact]
    public async Task Handle_PriceHistory_EscapesCommaQuoteAndNewlineAsync()
    {
        var point = new PriceTrendPointDto(
            new DateTimeOffset(2026, 7, 16, 10, 0, 0, TimeSpan.FromHours(7)),
            1.5m,
            1m,
            2m,
            2);
        var price = new PriceTrendsDto(
        [
            new PriceTrendSeriesDto(
                MarketProductId,
                "Fish, fresh",
                "he said \"hi\"",
                "hourly\nspecial",
                new PriceTrendSummaryDto(1m, 2m, 1.5m, null),
                [point])
        ]);
        var sender = CreateSender(priceResult: Result<PriceTrendsDto>.Success(price));

        var result = await sender.Send(PriceQuery());
        var csv = Text(result.Value);

        csv.Should().Contain("\"Fish, fresh\"");
        csv.Should().Contain("\"he said \"\"hi\"\"\"");
        csv.Should().Contain("\"hourly\nspecial\"");
    }

    [Fact]
    public async Task Handle_AnyDataset_StartsWithUtf8BomSoExcelDecodesVietnameseAsync()
    {
        var sender = CreateSender();

        var result = await sender.Send(PriceQuery());

        result.Value.Content.Should().StartWith(Encoding.UTF8.GetPreamble());
    }

    [Fact]
    public async Task Handle_AllDatasets_UseExactApiFieldHeadersAsync()
    {
        var sender = CreateSender();

        var price = await sender.Send(PriceQuery());
        var orders = await sender.Send(Query("order-history"));
        var delivery = await sender.Send(Query("delivery-performance"));

        Header(price.Value).Should().Be(
            "marketProductId,productName,marketName,interval,timestamp,avgPrice,minPrice,maxPrice,snapshotCount");
        Header(orders.Value).Should().Be("date,orderCount,revenueVND");
        Header(delivery.Value).Should().Be(
            "totalDeliveries,onTimeCount,lateCount,onTimeRatePercent,failedCount," +
            "avgDeliveryDurationMinutes,durationSampleCount," +
            "avgVehicleUtilizationPercent,utilizationSampleCount");
    }

    [Fact]
    public async Task Handle_DeliveryPerformance_UsesInvariantDecimalsAndEmptyNullableFieldsAsync()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("vi-VN");
            var sender = CreateSender(deliveryResult: Result<DeliveryPerformanceDto>.Success(
                new DeliveryPerformanceDto(1, 1, 0, 12.5m, 0, null, 0, 33.25m, 1)));

            var result = await sender.Send(Query("delivery-performance"));
            var rows = Text(result.Value).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

            rows[1].Should().Be("1,1,0,12.5,0,,0,33.25,1");
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Fact]
    public async Task Handle_EmptyPeriod_ReturnsHeaderOnlyAsync()
    {
        var sender = CreateSender();

        var result = await sender.Send(Query("order-history"));

        Text(result.Value).Should().Be("date,orderCount,revenueVND\r\n");
    }

    [Fact]
    public async Task Handle_OverFiftyThousandRows_ReturnsValidationErrorBeforeEnumerationAsync()
    {
        var price = new PriceTrendsDto(
        [
            new PriceTrendSeriesDto(
                MarketProductId,
                "Product",
                "Market",
                "hourly",
                new PriceTrendSummaryDto(1m, 1m, 1m, null),
                new OversizedPoints())
        ]);
        var sender = CreateSender(priceResult: Result<PriceTrendsDto>.Success(price));

        var result = await sender.Send(PriceQuery());

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("VALIDATION_ERROR");
        result.Error.Message.Should().Contain("narrow your from/to range");
    }

    [Fact]
    public async Task Handle_InnerQueryFailure_PropagatesErrorAsync()
    {
        var error = Error.Validation("VALIDATION_ERROR", "Inner query failed.");
        var sender = CreateSender(
            priceResult: Result<PriceTrendsDto>.Failure(error));

        var result = await sender.Send(PriceQuery());

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(error);
    }

    [Theory]
    [MemberData(nameof(InvalidQueries))]
    public async Task Validation_InvalidRequest_ThrowsValidationExceptionAsync(
        ExportAnalyticsQuery query)
    {
        var sender = CreateSender();

        var act = () => sender.Send(query);

        var exception = await act.Should().ThrowAsync<ValidationException>();
        exception.Which.Errors.Should().OnlyContain(error => error.ErrorCode == "VALIDATION_ERROR");
    }

    public static TheoryData<ExportAnalyticsQuery> InvalidQueries => new()
    {
        Query("unknown"),
        Query("order-history", format: "xlsx"),
        new ExportAnalyticsQuery("price-history", From, To, [], "csv"),
        new ExportAnalyticsQuery(
            "price-history",
            From,
            To,
            Enumerable.Range(0, 11).Select(_ => Guid.NewGuid()).ToArray(),
            "csv"),
        new ExportAnalyticsQuery("order-history", To, From, [], "csv"),
        new ExportAnalyticsQuery(
            "order-history",
            new DateOnly(2025, 1, 1),
            new DateOnly(2026, 1, 3),
            [],
            "csv")
    };

    private static ExportAnalyticsQuery PriceQuery() =>
        new("price-history", From, To, [MarketProductId], null);

    private static ExportAnalyticsQuery Query(string dataset, string? format = "csv") =>
        new(dataset, From, To, [], format);

    // The payload carries a UTF-8 BOM for Excel; these assertions are about the CSV text.
    private static string Text(CsvExportDto result) =>
        Encoding.UTF8.GetString(result.Content).TrimStart('﻿');

    private static string Header(CsvExportDto result) =>
        Text(result).Split("\r\n", StringSplitOptions.None)[0];

    private static ISender CreateSender(
        Result<PriceTrendsDto>? priceResult = null,
        Result<OrderMetricsDto>? orderResult = null,
        Result<DeliveryPerformanceDto>? deliveryResult = null)
    {
        priceResult ??= Result<PriceTrendsDto>.Success(new PriceTrendsDto([]));
        orderResult ??= Result<OrderMetricsDto>.Success(new OrderMetricsDto(
            new OrderMetricsSummaryDto(0, 0m, 0m, 0, 0m, 0, new Dictionary<string, int>()),
            []));
        deliveryResult ??= Result<DeliveryPerformanceDto>.Success(
            new DeliveryPerformanceDto(0, 0, 0, 0m, 0, null, 0, null, 0));

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAnalyticsModule(new ConfigurationBuilder().Build());
        services.AddSingleton<IRequestHandler<GetPriceTrendsQuery, Result<PriceTrendsDto>>>(
            new StubHandler<GetPriceTrendsQuery, PriceTrendsDto>(priceResult));
        services.AddSingleton<IRequestHandler<GetOrderMetricsQuery, Result<OrderMetricsDto>>>(
            new StubHandler<GetOrderMetricsQuery, OrderMetricsDto>(orderResult));
        services.AddSingleton<IRequestHandler<GetDeliveryPerformanceQuery, Result<DeliveryPerformanceDto>>>(
            new StubHandler<GetDeliveryPerformanceQuery, DeliveryPerformanceDto>(deliveryResult));
        return services.BuildServiceProvider().GetRequiredService<ISender>();
    }

    private sealed class StubHandler<TRequest, TValue>(Result<TValue> result)
        : IRequestHandler<TRequest, Result<TValue>>
        where TRequest : IRequest<Result<TValue>>
    {
        public Task<Result<TValue>> Handle(TRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(result);
    }

    private sealed class OversizedPoints : IReadOnlyList<PriceTrendPointDto>
    {
        public int Count => 50_001;

        public PriceTrendPointDto this[int index] =>
            throw new InvalidOperationException("Rows must not be enumerated above the cap.");

        public IEnumerator<PriceTrendPointDto> GetEnumerator() =>
            throw new InvalidOperationException("Rows must not be enumerated above the cap.");

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
