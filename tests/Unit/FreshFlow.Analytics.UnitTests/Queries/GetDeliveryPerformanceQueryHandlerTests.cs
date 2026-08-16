using FluentAssertions;
using FluentValidation;
using FreshFlow.Analytics.Application.Abstractions;
using FreshFlow.Analytics.Application.Queries.GetDeliveryPerformance;
using FreshFlow.Analytics.Infrastructure;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FreshFlow.Analytics.UnitTests.Queries;

[Trait("Category", "Unit")]
public sealed class GetDeliveryPerformanceQueryHandlerTests
{
    private static readonly DateOnly From = new(2026, 7, 16);
    private static readonly DateOnly To = new(2026, 7, 17);

    [Fact]
    public async Task Handle_NormalSet_ReturnsCountsRatesSamplesAndVietnamBoundsAsync()
    {
        var reader = new StubDeliveryPerformanceReader(new DeliveryPerformanceReadModel(
            3,
            1,
            1,
            2,
            105m,
            2,
            [50m, 150m]));
        var sender = CreateSender(reader);

        var result = await sender.Send(new GetDeliveryPerformanceQuery(From, To));

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalDeliveries.Should().Be(3);
        result.Value.OnTimeCount.Should().Be(1);
        result.Value.LateCount.Should().Be(1);
        result.Value.OnTimeRatePercent.Should().Be(50m);
        result.Value.FailedCount.Should().Be(2);
        result.Value.AvgDeliveryDurationMinutes.Should().Be(105m);
        result.Value.DurationSampleCount.Should().Be(2);
        result.Value.AvgVehicleUtilizationPercent.Should().Be(75m);
        result.Value.UtilizationSampleCount.Should().Be(2);
        reader.StartUtc.Should().Be(new DateTime(2026, 7, 15, 17, 0, 0, DateTimeKind.Utc));
        reader.EndUtc.Should().Be(new DateTime(2026, 7, 17, 17, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task Handle_1459And1501ThresholdSides_ReturnsOneOnTimeAndOneLateAsync()
    {
        var sender = CreateSender(new StubDeliveryPerformanceReader(
            new DeliveryPerformanceReadModel(2, 1, 1, 0, null, 0, [])));

        var result = await sender.Send(new GetDeliveryPerformanceQuery(From, From));

        result.Value.OnTimeCount.Should().Be(1);
        result.Value.LateCount.Should().Be(1);
        result.Value.OnTimeRatePercent.Should().Be(50m);
    }

    [Fact]
    public async Task Handle_FailedDeliveries_DoNotEnterOnTimeRateAsync()
    {
        var sender = CreateSender(new StubDeliveryPerformanceReader(
            new DeliveryPerformanceReadModel(1, 1, 0, 9, null, 0, [])));

        var result = await sender.Send(new GetDeliveryPerformanceQuery(From, From));

        result.Value.FailedCount.Should().Be(9);
        result.Value.OnTimeRatePercent.Should().Be(100m);
    }

    [Fact]
    public async Task Handle_NoDeliveries_ReturnsZerosAndNullSamplesAsync()
    {
        var sender = CreateSender(new StubDeliveryPerformanceReader(EmptyMetrics()));

        var result = await sender.Send(new GetDeliveryPerformanceQuery(From, To));

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalDeliveries.Should().Be(0);
        result.Value.OnTimeCount.Should().Be(0);
        result.Value.LateCount.Should().Be(0);
        result.Value.OnTimeRatePercent.Should().Be(0m);
        result.Value.FailedCount.Should().Be(0);
        result.Value.AvgDeliveryDurationMinutes.Should().BeNull();
        result.Value.DurationSampleCount.Should().Be(0);
        result.Value.AvgVehicleUtilizationPercent.Should().BeNull();
        result.Value.UtilizationSampleCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_RouteWithoutCheckedOutHandover_IsNotDurationZeroSampleAsync()
    {
        var sender = CreateSender(new StubDeliveryPerformanceReader(
            new DeliveryPerformanceReadModel(1, 1, 0, 0, null, 0, [])));

        var result = await sender.Send(new GetDeliveryPerformanceQuery(From, From));

        result.Value.AvgDeliveryDurationMinutes.Should().BeNull();
        result.Value.DurationSampleCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_CheckedOutHandoverWithoutDriverConfirmation_IsExcludedAsync()
    {
        var sender = CreateSender(new StubDeliveryPerformanceReader(
            new DeliveryPerformanceReadModel(1, 1, 0, 0, null, 0, [])));

        var result = await sender.Send(new GetDeliveryPerformanceQuery(From, From));

        result.Value.AvgDeliveryDurationMinutes.Should().BeNull();
        result.Value.DurationSampleCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_RouteWithoutVehicle_IsExcludedFromUtilizationAsync()
    {
        var sender = CreateSender(new StubDeliveryPerformanceReader(
            new DeliveryPerformanceReadModel(1, 1, 0, 0, null, 0, [])));

        var result = await sender.Send(new GetDeliveryPerformanceQuery(From, From));

        result.Value.AvgVehicleUtilizationPercent.Should().BeNull();
        result.Value.UtilizationSampleCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ZeroCapacityVehicle_IsExcludedWithoutDivisionByZeroAsync()
    {
        var sender = CreateSender(new StubDeliveryPerformanceReader(
            new DeliveryPerformanceReadModel(1, 1, 0, 0, null, 0, [])));

        var result = await sender.Send(new GetDeliveryPerformanceQuery(From, From));

        result.Value.AvgVehicleUtilizationPercent.Should().BeNull();
        result.Value.UtilizationSampleCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_UtilizationAboveOneHundred_ClampsEachRouteAsync()
    {
        var sender = CreateSender(new StubDeliveryPerformanceReader(
            new DeliveryPerformanceReadModel(1, 1, 0, 0, null, 0, [150m, 50m])));

        var result = await sender.Send(new GetDeliveryPerformanceQuery(From, From));

        result.Value.AvgVehicleUtilizationPercent.Should().Be(75m);
        result.Value.UtilizationSampleCount.Should().Be(2);
    }

    [Theory]
    [MemberData(nameof(InvalidQueries))]
    public async Task Validation_InvalidRange_ThrowsValidationErrorAsync(
        GetDeliveryPerformanceQuery query)
    {
        var sender = CreateSender(new StubDeliveryPerformanceReader(EmptyMetrics()));

        var act = () => sender.Send(query);

        var exception = await act.Should().ThrowAsync<ValidationException>();
        exception.Which.Errors.Should().OnlyContain(error => error.ErrorCode == "VALIDATION_ERROR");
    }

    public static TheoryData<GetDeliveryPerformanceQuery> InvalidQueries => new()
    {
        new GetDeliveryPerformanceQuery(
            new DateOnly(2026, 7, 2),
            new DateOnly(2026, 7, 1)),
        new GetDeliveryPerformanceQuery(
            new DateOnly(2025, 1, 1),
            new DateOnly(2026, 1, 3))
    };

    private static DeliveryPerformanceReadModel EmptyMetrics() =>
        new(0, 0, 0, 0, null, 0, []);

    private static ISender CreateSender(IDeliveryPerformanceReader reader)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAnalyticsModule(new ConfigurationBuilder().Build());
        services.AddSingleton(reader);
        return services.BuildServiceProvider().GetRequiredService<ISender>();
    }

    private sealed class StubDeliveryPerformanceReader(DeliveryPerformanceReadModel result)
        : IDeliveryPerformanceReader
    {
        public DateTime StartUtc { get; private set; }
        public DateTime EndUtc { get; private set; }

        public Task<DeliveryPerformanceReadModel> ReadAsync(
            DateTime startUtcInclusive,
            DateTime endUtcExclusive,
            CancellationToken ct)
        {
            StartUtc = startUtcInclusive;
            EndUtc = endUtcExclusive;
            return Task.FromResult(result);
        }
    }
}
