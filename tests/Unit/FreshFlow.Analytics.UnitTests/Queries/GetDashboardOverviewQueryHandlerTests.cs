using FluentAssertions;
using FreshFlow.Analytics.Application.Abstractions;
using FreshFlow.Analytics.Application.Queries.GetDashboardOverview;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace FreshFlow.Analytics.UnitTests.Queries;

[Trait("Category", "Unit")]
public sealed class GetDashboardOverviewQueryHandlerTests
{
    [Fact]
    public async Task Handle_HappyPath_ReturnsAllKpisAsync()
    {
        var reader = new StubDashboardOverviewReader(new DashboardOverviewReadModel(
            12,
            1_250_000m,
            5,
            2,
            3,
            4,
            3,
            145.5m,
            98.25m));
        var sender = CreateSender(reader, new DateTimeOffset(2026, 7, 16, 0, 0, 0, TimeSpan.Zero));

        var result = await sender.Send(new GetDashboardOverviewQuery(new DateOnly(2026, 7, 16)));

        result.IsSuccess.Should().BeTrue();
        result.Value.OrdersToday.Should().Be(12);
        result.Value.RevenueToday.Should().Be(1_250_000m);
        result.Value.PendingOrders.Should().Be(5);
        result.Value.CancelledToday.Should().Be(2);
        result.Value.ActiveProcurementBatches.Should().Be(3);
        result.Value.DeliveriesToday.Should().Be(4);
        result.Value.OnTimeRatePercent.Should().Be(75m);
        result.Value.HubInboundKgToday.Should().Be(145.5m);
        result.Value.HubOutboundKgToday.Should().Be(98.25m);
    }

    [Fact]
    public async Task Handle_EmptyDay_ReturnsExplicitZerosAsync()
    {
        var reader = new StubDashboardOverviewReader(new DashboardOverviewReadModel(
            0,
            0m,
            0,
            0,
            0,
            0,
            0,
            0m,
            0m));
        var sender = CreateSender(reader, new DateTimeOffset(2026, 7, 16, 0, 0, 0, TimeSpan.Zero));

        var result = await sender.Send(new GetDashboardOverviewQuery(new DateOnly(2026, 7, 16)));

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEquivalentTo(new
        {
            OrdersToday = 0,
            RevenueToday = 0m,
            PendingOrders = 0,
            CancelledToday = 0,
            ActiveProcurementBatches = 0,
            DeliveriesToday = 0,
            OnTimeRatePercent = 0m,
            HubInboundKgToday = 0m,
            HubOutboundKgToday = 0m
        });
    }

    [Fact]
    public async Task Handle_UsesVietnamBusinessDayAndUtcBoundsAsync()
    {
        var reader = new StubDashboardOverviewReader(new DashboardOverviewReadModel(
            0,
            0m,
            0,
            0,
            0,
            0,
            0,
            0m,
            0m));
        var sender = CreateSender(reader, new DateTimeOffset(2026, 7, 16, 18, 0, 0, TimeSpan.Zero));

        await sender.Send(new GetDashboardOverviewQuery());

        reader.Date.Should().Be(new DateOnly(2026, 7, 17));
        reader.StartUtc.Should().Be(new DateTime(2026, 7, 16, 17, 0, 0, DateTimeKind.Utc));
        reader.EndUtc.Should().Be(new DateTime(2026, 7, 17, 17, 0, 0, DateTimeKind.Utc));

        var orderAt2330Vietnam = new DateTime(2026, 7, 17, 16, 30, 0, DateTimeKind.Utc);
        orderAt2330Vietnam.Should().BeOnOrAfter(reader.StartUtc);
        orderAt2330Vietnam.Should().BeBefore(reader.EndUtc);
    }

    private static ISender CreateSender(
        IDashboardOverviewReader reader,
        DateTimeOffset utcNow)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(reader);
        services.AddSingleton<TimeProvider>(new FixedTimeProvider(utcNow));
        services.AddMediatR(config =>
            config.RegisterServicesFromAssembly(typeof(GetDashboardOverviewQuery).Assembly));

        return services.BuildServiceProvider().GetRequiredService<ISender>();
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class StubDashboardOverviewReader(DashboardOverviewReadModel result)
        : IDashboardOverviewReader
    {
        public DateOnly Date { get; private set; }
        public DateTime StartUtc { get; private set; }
        public DateTime EndUtc { get; private set; }

        public Task<DashboardOverviewReadModel> ReadAsync(
            DateOnly date,
            DateTime startUtcInclusive,
            DateTime endUtcExclusive,
            CancellationToken ct)
        {
            Date = date;
            StartUtc = startUtcInclusive;
            EndUtc = endUtcExclusive;
            return Task.FromResult(result);
        }
    }
}

