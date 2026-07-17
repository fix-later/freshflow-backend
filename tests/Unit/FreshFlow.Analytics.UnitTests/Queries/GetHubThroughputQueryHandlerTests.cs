using FluentAssertions;
using FluentValidation;
using FreshFlow.Analytics.Application.Abstractions;
using FreshFlow.Analytics.Application.Dtos;
using FreshFlow.Analytics.Application.Queries.GetHubThroughput;
using FreshFlow.Analytics.Infrastructure;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FreshFlow.Analytics.UnitTests.Queries;

[Trait("Category", "Unit")]
public sealed class GetHubThroughputQueryHandlerTests
{
    private static readonly Guid HubA = Guid.NewGuid();
    private static readonly Guid HubB = Guid.NewGuid();
    private static readonly DateOnly From = new(2026, 7, 16);
    private static readonly DateOnly To = new(2026, 7, 17);

    [Fact]
    public async Task Handle_NormalSet_ReturnsSummaryOrderedBucketsAndVietnamBoundsAsync()
    {
        var reader = new StubHubThroughputReader(new HubThroughputReadModel(
            [
                new(new DateOnly(2026, 7, 17), HubB, "Beta Hub", 20m, 1),
                new(new DateOnly(2026, 7, 16), HubB, "Beta Hub", 5m, 1),
                new(new DateOnly(2026, 7, 16), HubA, "Alpha Hub", 10m, 2)
            ],
            [
                new(new DateOnly(2026, 7, 16), HubA, "Alpha Hub", 15m, 1),
                new(new DateOnly(2026, 7, 17), HubB, "Beta Hub", 30m, 1),
                new(new DateOnly(2026, 7, 16), HubB, "Beta Hub", 2m, 1)
            ],
            [
                new("PENDING", 1),
                new("ARRIVED_AT_HUB", 3)
            ]));
        var sender = CreateSender(reader);

        var result = await sender.Send(new GetHubThroughputQuery(From, To, HubA));

        result.IsSuccess.Should().BeTrue();
        result.Value.Summary.InboundKg.Should().Be(35m);
        result.Value.Summary.OutboundKg.Should().Be(47m);
        result.Value.Summary.NetKg.Should().Be(-12m);
        result.Value.Summary.InboundEventCount.Should().Be(4);
        result.Value.Summary.OutboundEventCount.Should().Be(3);
        result.Value.Summary.InboundStatusCounts.Should().Contain(new Dictionary<string, int>
        {
            ["PENDING"] = 1,
            ["ARRIVED_AT_HUB"] = 3
        });
        result.Value.Buckets.Should().Equal(
            new HubThroughputBucketDto(
                new DateOnly(2026, 7, 16), HubA, "Alpha Hub", 10m, 15m, 2, 1),
            new HubThroughputBucketDto(
                new DateOnly(2026, 7, 16), HubB, "Beta Hub", 5m, 2m, 1, 1),
            new HubThroughputBucketDto(
                new DateOnly(2026, 7, 17), HubB, "Beta Hub", 20m, 30m, 1, 1));
        reader.StartUtc.Should().Be(new DateTime(2026, 7, 15, 17, 0, 0, DateTimeKind.Utc));
        reader.EndUtc.Should().Be(new DateTime(2026, 7, 17, 17, 0, 0, DateTimeKind.Utc));
        reader.HubId.Should().Be(HubA);
    }

    [Fact]
    public async Task Handle_NoEvents_ReturnsZerosAllStatusesAndNoBucketsAsync()
    {
        var sender = CreateSender(new StubHubThroughputReader(EmptyMetrics()));

        var result = await sender.Send(new GetHubThroughputQuery(From, To, null));

        result.IsSuccess.Should().BeTrue();
        result.Value.Summary.InboundKg.Should().Be(0m);
        result.Value.Summary.OutboundKg.Should().Be(0m);
        result.Value.Summary.NetKg.Should().Be(0m);
        result.Value.Summary.InboundEventCount.Should().Be(0);
        result.Value.Summary.OutboundEventCount.Should().Be(0);
        result.Value.Summary.InboundStatusCounts.Should().HaveCount(2);
        result.Value.Summary.InboundStatusCounts.Values.Should().OnlyContain(count => count == 0);
        result.Value.Buckets.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_PendingInbound_CountsEventButNotKgAndKeepsArrivedZeroAsync()
    {
        var sender = CreateSender(new StubHubThroughputReader(new HubThroughputReadModel(
            [new(From, HubA, "Alpha Hub", 0m, 1)],
            [],
            [new("PENDING", 1)])));

        var result = await sender.Send(new GetHubThroughputQuery(From, From, HubA));

        result.Value.Summary.InboundKg.Should().Be(0m);
        result.Value.Summary.InboundEventCount.Should().Be(1);
        result.Value.Summary.InboundStatusCounts["PENDING"].Should().Be(1);
        result.Value.Summary.InboundStatusCounts["ARRIVED_AT_HUB"].Should().Be(0);
        result.Value.Buckets.Should().ContainSingle().Which.Should().Be(
            new HubThroughputBucketDto(From, HubA, "Alpha Hub", 0m, 0m, 1, 0));
    }

    [Fact]
    public async Task Handle_OutboundExceedsInbound_ReturnsNegativeNetKgAsync()
    {
        var sender = CreateSender(new StubHubThroughputReader(new HubThroughputReadModel(
            [new(From, HubA, "Alpha Hub", 5m, 1)],
            [new(From, HubA, "Alpha Hub", 8m, 1)],
            [new("ARRIVED_AT_HUB", 1)])));

        var result = await sender.Send(new GetHubThroughputQuery(From, From, HubA));

        result.Value.Summary.NetKg.Should().Be(-3m);
    }

    [Theory]
    [MemberData(nameof(InvalidQueries))]
    public async Task Validation_InvalidRange_ThrowsValidationErrorAsync(GetHubThroughputQuery query)
    {
        var sender = CreateSender(new StubHubThroughputReader(EmptyMetrics()));

        var act = () => sender.Send(query);

        var exception = await act.Should().ThrowAsync<ValidationException>();
        exception.Which.Errors.Should().OnlyContain(error => error.ErrorCode == "VALIDATION_ERROR");
    }

    public static TheoryData<GetHubThroughputQuery> InvalidQueries => new()
    {
        new GetHubThroughputQuery(
            new DateOnly(2026, 7, 2),
            new DateOnly(2026, 7, 1),
            null),
        new GetHubThroughputQuery(
            new DateOnly(2025, 1, 1),
            new DateOnly(2026, 1, 3),
            null)
    };

    private static HubThroughputReadModel EmptyMetrics() => new([], [], []);

    private static ISender CreateSender(IHubThroughputReader reader)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAnalyticsModule(new ConfigurationBuilder().Build());
        services.AddSingleton(reader);
        return services.BuildServiceProvider().GetRequiredService<ISender>();
    }

    private sealed class StubHubThroughputReader(HubThroughputReadModel result)
        : IHubThroughputReader
    {
        public DateTime StartUtc { get; private set; }
        public DateTime EndUtc { get; private set; }
        public Guid? HubId { get; private set; }

        public Task<HubThroughputReadModel> ReadAsync(
            DateTime startUtcInclusive,
            DateTime endUtcExclusive,
            Guid? hubId,
            CancellationToken ct)
        {
            StartUtc = startUtcInclusive;
            EndUtc = endUtcExclusive;
            HubId = hubId;
            return Task.FromResult(result);
        }
    }
}
