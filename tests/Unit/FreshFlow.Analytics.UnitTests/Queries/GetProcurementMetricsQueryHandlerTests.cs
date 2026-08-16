using FluentAssertions;
using FluentValidation;
using FreshFlow.Analytics.Application.Abstractions;
using FreshFlow.Analytics.Application.Queries.GetProcurementMetrics;
using FreshFlow.Analytics.Infrastructure;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FreshFlow.Analytics.UnitTests.Queries;

[Trait("Category", "Unit")]
public sealed class GetProcurementMetricsQueryHandlerTests
{
    private static readonly Guid MarketId = Guid.NewGuid();
    private static readonly DateOnly From = new(2026, 7, 1);
    private static readonly DateOnly To = new(2026, 7, 16);

    [Fact]
    public async Task Handle_NormalSet_ReturnsCorrectMetricsAndDateOnlyBoundsAsync()
    {
        var reader = new StubProcurementMetricsReader(new ProcurementMetricsReadModel(
            [
                new("Built", 2),
                new("HandedOff", 1)
            ],
            5,
            3,
            1_050m,
            5.126m,
            104.666m,
            [
                new("Unavailable", 2),
                new("Damaged", 1)
            ]));
        var sender = CreateSender(reader);

        var result = await sender.Send(new GetProcurementMetricsQuery(From, To, MarketId));

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalBatches.Should().Be(3);
        result.Value.CompletionRatePercent.Should().Be(33.33m);
        result.Value.ItemsTotal.Should().Be(5);
        result.Value.ItemsPurchased.Should().Be(3);
        result.Value.ItemsPending.Should().Be(2);
        result.Value.TotalActualCostVND.Should().Be(1_050m);
        result.Value.PriceVariancePercent.Should().Be(5.13m);
        result.Value.AvgLeadTimeMinutes.Should().Be(104.67m);
        result.Value.ExceptionCount.Should().Be(3);
        result.Value.StatusCounts.Should().Contain(new Dictionary<string, int>
        {
            ["Built"] = 2,
            ["Manifested"] = 0,
            ["Purchasing"] = 0,
            ["HandedOff"] = 1
        });
        result.Value.ExceptionsByType.Should().Contain(new Dictionary<string, int>
        {
            ["Unavailable"] = 2,
            ["Shortfall"] = 0,
            ["PriceDiscrepancy"] = 0,
            ["Damaged"] = 1
        });
        reader.From.Should().Be(From);
        reader.To.Should().Be(To);
        reader.MarketId.Should().Be(MarketId);
    }

    [Fact]
    public async Task Handle_CancelledBatches_CountsThemSoStatusCountsSumToTotalAsync()
    {
        var reader = new StubProcurementMetricsReader(new ProcurementMetricsReadModel(
            [
                new("Built", 2),
                new("HandedOff", 1),
                new("Cancelled", 3)
            ],
            5,
            3,
            1_050m,
            null,
            null,
            []));
        var sender = CreateSender(reader);

        var result = await sender.Send(new GetProcurementMetricsQuery(From, To, null));

        result.IsSuccess.Should().BeTrue();
        result.Value.StatusCounts["Cancelled"].Should().Be(3);
        result.Value.StatusCounts.Values.Sum().Should().Be(result.Value.TotalBatches);
    }

    [Fact]
    public async Task Handle_NoBatches_ReturnsZerosNullSamplesAndAllKeysAsync()
    {
        var sender = CreateSender(new StubProcurementMetricsReader(EmptyMetrics()));

        var result = await sender.Send(new GetProcurementMetricsQuery(From, To, null));

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalBatches.Should().Be(0);
        result.Value.CompletionRatePercent.Should().Be(0m);
        result.Value.ItemsTotal.Should().Be(0);
        result.Value.ItemsPurchased.Should().Be(0);
        result.Value.ItemsPending.Should().Be(0);
        result.Value.TotalActualCostVND.Should().Be(0m);
        result.Value.PriceVariancePercent.Should().BeNull();
        result.Value.AvgLeadTimeMinutes.Should().BeNull();
        result.Value.ExceptionCount.Should().Be(0);
        result.Value.StatusCounts.Should().HaveCount(6);
        result.Value.StatusCounts.Values.Should().OnlyContain(count => count == 0);
        result.Value.ExceptionsByType.Should().HaveCount(4);
        result.Value.ExceptionsByType.Values.Should().OnlyContain(count => count == 0);
    }

    [Fact]
    public async Task Handle_BatchNotYetPurchased_ReturnsPendingItemAsync()
    {
        var sender = CreateSender(new StubProcurementMetricsReader(new ProcurementMetricsReadModel(
            [new("Built", 1)],
            1,
            0,
            0m,
            null,
            null,
            [])));

        var result = await sender.Send(new GetProcurementMetricsQuery(From, To, null));

        result.Value.ItemsTotal.Should().Be(1);
        result.Value.ItemsPurchased.Should().Be(0);
        result.Value.ItemsPending.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ActualPriceMissing_KeepsPurchasedCountButNoCostOrVarianceAsync()
    {
        var sender = CreateSender(new StubProcurementMetricsReader(new ProcurementMetricsReadModel(
            [new("Purchasing", 1)],
            1,
            1,
            0m,
            null,
            null,
            [])));

        var result = await sender.Send(new GetProcurementMetricsQuery(From, To, null));

        result.Value.ItemsPurchased.Should().Be(1);
        result.Value.ItemsPending.Should().Be(0);
        result.Value.TotalActualCostVND.Should().Be(0m);
        result.Value.PriceVariancePercent.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ZeroReferenceAndIncompleteLeadSamples_ReturnNullOptionalMetricsAsync()
    {
        var sender = CreateSender(new StubProcurementMetricsReader(new ProcurementMetricsReadModel(
            [new("Manifested", 1)],
            1,
            1,
            100m,
            null,
            null,
            [])));

        var result = await sender.Send(new GetProcurementMetricsQuery(From, To, null));

        result.Value.TotalActualCostVND.Should().Be(100m);
        result.Value.PriceVariancePercent.Should().BeNull();
        result.Value.AvgLeadTimeMinutes.Should().BeNull();
    }

    [Fact]
    public async Task Handle_CompleteLeadSample_ReturnsAverageAsync()
    {
        var sender = CreateSender(new StubProcurementMetricsReader(new ProcurementMetricsReadModel(
            [new("HandedOff", 1)],
            0,
            0,
            0m,
            null,
            90m,
            [])));

        var result = await sender.Send(new GetProcurementMetricsQuery(From, To, null));

        result.Value.AvgLeadTimeMinutes.Should().Be(90m);
    }

    [Theory]
    [MemberData(nameof(InvalidQueries))]
    public async Task Validation_InvalidRange_ThrowsValidationErrorAsync(
        GetProcurementMetricsQuery query)
    {
        var sender = CreateSender(new StubProcurementMetricsReader(EmptyMetrics()));

        var act = () => sender.Send(query);

        var exception = await act.Should().ThrowAsync<ValidationException>();
        exception.Which.Errors.Should().OnlyContain(error => error.ErrorCode == "VALIDATION_ERROR");
    }

    public static TheoryData<GetProcurementMetricsQuery> InvalidQueries => new()
    {
        new GetProcurementMetricsQuery(
            new DateOnly(2026, 7, 2),
            new DateOnly(2026, 7, 1),
            null),
        new GetProcurementMetricsQuery(
            new DateOnly(2025, 1, 1),
            new DateOnly(2026, 1, 3),
            null)
    };

    private static ProcurementMetricsReadModel EmptyMetrics() => new(
        [],
        0,
        0,
        0m,
        null,
        null,
        []);

    private static ISender CreateSender(IProcurementMetricsReader reader)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAnalyticsModule(new ConfigurationBuilder().Build());
        services.AddSingleton(reader);
        return services.BuildServiceProvider().GetRequiredService<ISender>();
    }

    private sealed class StubProcurementMetricsReader(
        ProcurementMetricsReadModel result) : IProcurementMetricsReader
    {
        public DateOnly From { get; private set; }
        public DateOnly To { get; private set; }
        public Guid? MarketId { get; private set; }

        public Task<ProcurementMetricsReadModel> ReadAsync(
            DateOnly fromInclusive,
            DateOnly toInclusive,
            Guid? marketId,
            CancellationToken ct)
        {
            From = fromInclusive;
            To = toInclusive;
            MarketId = marketId;
            return Task.FromResult(result);
        }
    }
}
