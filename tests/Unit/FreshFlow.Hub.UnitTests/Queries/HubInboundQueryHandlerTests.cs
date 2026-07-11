using FluentAssertions;
using FreshFlow.Hub.Application.Queries.GetPendingInbound;
using FreshFlow.Hub.Application.Queries.ListInbound;
using FreshFlow.Hub.Domain.Entities;
using FreshFlow.Hub.UnitTests.TestDoubles;
using HubEntity = FreshFlow.Hub.Domain.Entities.Hub;

namespace FreshFlow.Hub.UnitTests.Queries;

[Trait("Category", "Unit")]
public sealed class HubInboundQueryHandlerTests
{
    [Fact]
    public async Task GetPendingInbound_ReturnsPendingAndArrivedEventsAsync()
    {
        var hubs = new InMemoryHubRepository();
        var inbounds = new InMemoryHubInboundRepository();
        var hub = HubEntity.Create("Main Hub", null, null, null, 1000, null);
        var pending = CreateInbound(hub.Id, DateTime.UtcNow, 10m);
        var arrived = CreateInbound(hub.Id, DateTime.UtcNow, 20m);
        arrived.ConfirmArrival();
        await hubs.AddAsync(hub, default);
        await inbounds.AddAsync(pending, default);
        await inbounds.AddAsync(arrived, default);
        var sut = new GetPendingInboundQueryHandler(hubs, inbounds);

        var result = await sut.Handle(new GetPendingInboundQuery(hub.Id), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(2);
        result.Value.Items.Select(i => i.Status).Should()
            .BeEquivalentTo([HubInboundEvent.StatusPending, HubInboundEvent.StatusArrivedAtHub]);
        result.Value.TotalQuantityKg.Should().Be(30m);
    }

    [Fact]
    public async Task ListInbound_DateFilter_ReturnsHistoryAndTotalsAsync()
    {
        var hubs = new InMemoryHubRepository();
        var inbounds = new InMemoryHubInboundRepository();
        var hub = HubEntity.Create("Main Hub", null, null, null, 1000, null);
        var date = new DateOnly(2026, 7, 10);
        await hubs.AddAsync(hub, default);
        await inbounds.AddAsync(CreateInbound(hub.Id, date.ToDateTime(TimeOnly.FromTimeSpan(TimeSpan.FromHours(9))), 10m), default);
        await inbounds.AddAsync(CreateInbound(hub.Id, date.AddDays(-1).ToDateTime(TimeOnly.FromTimeSpan(TimeSpan.FromHours(9))), 15m), default);
        var sut = new ListInboundQueryHandler(hubs, inbounds);

        var result = await sut.Handle(new ListInboundQuery(hub.Id, date), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().ContainSingle();
        result.Value.TotalQuantityKg.Should().Be(10m);
    }

    [Fact]
    public async Task ListInbound_MissingHub_ReturnsHubNotFoundAsync()
    {
        var sut = new ListInboundQueryHandler(
            new InMemoryHubRepository(),
            new InMemoryHubInboundRepository());

        var result = await sut.Handle(new ListInboundQuery(Guid.NewGuid()), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("HUB_NOT_FOUND");
    }

    private static HubInboundEvent CreateInbound(Guid hubId, DateTime arrivedAt, decimal quantityKg) =>
        HubInboundEvent.Record(
            hubId,
            null,
            null,
            null,
            [new HubInboundItem(Guid.NewGuid(), null, quantityKg)],
            arrivedAt);
}
