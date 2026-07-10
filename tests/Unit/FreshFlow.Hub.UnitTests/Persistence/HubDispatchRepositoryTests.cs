using FluentAssertions;
using FreshFlow.Hub.Domain.Entities;
using FreshFlow.Hub.Infrastructure.Repositories;
using FreshFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using HubEntity = FreshFlow.Hub.Domain.Entities.Hub;

namespace FreshFlow.Hub.UnitTests.Persistence;

[Trait("Category", "Unit")]
public sealed class HubDispatchRepositoryTests
{
    [Fact]
    public async Task CrossDockRepository_GetPageAsync_FiltersByHubAndStatusAsync()
    {
        using var db = CreateContext();
        var hubs = new HubRepository(db);
        var sut = new CrossDockRepository(db);
        var hub = HubEntity.Create("Main Hub", null, null, null, 1000, null);
        var otherHub = HubEntity.Create("Other Hub", null, null, null, 1000, null);
        var inbound = CreateArrivedInbound(hub.Id);
        var otherInbound = CreateArrivedInbound(otherHub.Id);
        var transfer = CrossDockTransfer.Create(hub.Id, inbound.Id, Guid.NewGuid(), null);
        await hubs.AddAsync(hub, default);
        await hubs.AddAsync(otherHub, default);
        await db.Set<HubInboundEvent>().AddRangeAsync(inbound, otherInbound);
        await sut.AddAsync(transfer, default);
        await sut.AddAsync(CrossDockTransfer.Create(otherHub.Id, otherInbound.Id, Guid.NewGuid(), null), default);
        await sut.SaveChangesAsync(default);

        var result = await sut.GetPageAsync(hub.Id, CrossDockTransfer.StatusPending, null, 10, default);

        result.Items.Should().ContainSingle()
            .Which.Id.Should().Be(transfer.Id);
    }

    [Fact]
    public async Task HubOutboundRepository_GetHistoryPageAsync_FiltersByDateAndTotalsAsync()
    {
        using var db = CreateContext();
        var hubs = new HubRepository(db);
        var sut = new HubOutboundRepository(db);
        var hub = HubEntity.Create("Main Hub", null, null, null, 1000, null);
        var marketProductId = Guid.NewGuid();
        var date = new DateOnly(2026, 7, 11);
        await hubs.AddAsync(hub, default);
        await sut.AddAsync(
            HubOutboundEvent.Record(
                hub.Id,
                Guid.NewGuid(),
                [new HubOutboundItem(marketProductId, null, 4m)],
                date.ToDateTime(TimeOnly.MinValue)),
            default);
        await sut.AddAsync(
            HubOutboundEvent.Record(
                hub.Id,
                Guid.NewGuid(),
                [new HubOutboundItem(marketProductId, null, 6m)],
                date.AddDays(-1).ToDateTime(TimeOnly.MinValue)),
            default);
        await sut.SaveChangesAsync(default);

        var result = await sut.GetHistoryPageAsync(hub.Id, date, null, 10, default);

        result.Items.Should().ContainSingle();
        result.TotalQuantityKg.Should().Be(4m);
    }

    [Fact]
    public async Task HubOutboundRepository_GetHistoryPageAsync_MoreRowsThanPageSize_ReturnsNextCursorAsync()
    {
        using var db = CreateContext();
        var hubs = new HubRepository(db);
        var sut = new HubOutboundRepository(db);
        var hub = HubEntity.Create("Main Hub", null, null, null, 1000, null);
        await hubs.AddAsync(hub, default);
        await sut.AddAsync(CreateOutbound(hub.Id, 1m), default);
        await sut.AddAsync(CreateOutbound(hub.Id, 2m), default);
        await sut.SaveChangesAsync(default);

        var result = await sut.GetHistoryPageAsync(hub.Id, null, null, 1, default);

        result.Items.Should().ContainSingle();
        result.NextCursor.Should().NotBeNull();
        result.TotalQuantityKg.Should().Be(3m);
    }

    private static AppDbContext CreateContext()
    {
        _ = typeof(FreshFlow.Hub.Infrastructure.DependencyInjection).Assembly;

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"hub-dispatch-repository-{Guid.NewGuid()}")
            .Options;

        return new AppDbContext(options);
    }

    private static HubInboundEvent CreateArrivedInbound(Guid hubId)
    {
        var inbound = HubInboundEvent.Record(
            hubId,
            null,
            null,
            null,
            [new HubInboundItem(Guid.NewGuid(), null, 1m)],
            DateTime.UtcNow);
        inbound.ConfirmArrival();
        return inbound;
    }

    private static HubOutboundEvent CreateOutbound(Guid hubId, decimal quantityKg) =>
        HubOutboundEvent.Record(
            hubId,
            Guid.NewGuid(),
            [new HubOutboundItem(Guid.NewGuid(), null, quantityKg)],
            DateTime.UtcNow);
}
