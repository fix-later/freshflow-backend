using FluentAssertions;
using FreshFlow.Hub.Application.Commands.CreateCrossDock;
using FreshFlow.Hub.Application.Commands.RecordOutbound;
using FreshFlow.Hub.Application.Queries.ListCrossDock;
using FreshFlow.Hub.Application.Queries.ListOutbound;
using FreshFlow.Hub.Domain.Entities;
using FreshFlow.Hub.UnitTests.TestDoubles;
using HubEntity = FreshFlow.Hub.Domain.Entities.Hub;

namespace FreshFlow.Hub.UnitTests.Commands;

[Trait("Category", "Unit")]
public sealed class HubDispatchCommandHandlerTests
{
    [Fact]
    public async Task CreateCrossDock_ArrivedInboundAndExistingRoute_CreatesPendingTransferAsync()
    {
        var hubs = new InMemoryHubRepository();
        var inbounds = new InMemoryHubInboundRepository();
        var transfers = new InMemoryCrossDockRepository();
        var routes = new InMemoryDeliveryRouteReader();
        var hub = HubEntity.Create("Main Hub", null, null, null, 1000, null);
        var inbound = CreateInbound(hub.Id);
        inbound.ConfirmArrival();
        var routeId = Guid.NewGuid();
        await hubs.AddAsync(hub, default);
        await inbounds.AddAsync(inbound, default);
        routes.Add(routeId);
        var sut = new CreateCrossDockCommandHandler(hubs, inbounds, transfers, routes);

        var result = await sut.Handle(
            new CreateCrossDockCommand(hub.Id, inbound.Id, routeId, "Dock 1"),
            default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(CrossDockTransfer.StatusPending);
        result.Value.Notes.Should().Be("Dock 1");
        transfers.Transfers.Should().ContainSingle();
        transfers.SaveChangesCount.Should().Be(1);
    }

    [Fact]
    public async Task CreateCrossDock_PendingInbound_ReturnsInboundNotArrivedAsync()
    {
        var hubs = new InMemoryHubRepository();
        var inbounds = new InMemoryHubInboundRepository();
        var transfers = new InMemoryCrossDockRepository();
        var routes = new InMemoryDeliveryRouteReader();
        var hub = HubEntity.Create("Main Hub", null, null, null, 1000, null);
        var inbound = CreateInbound(hub.Id);
        var routeId = Guid.NewGuid();
        await hubs.AddAsync(hub, default);
        await inbounds.AddAsync(inbound, default);
        routes.Add(routeId);
        var sut = new CreateCrossDockCommandHandler(hubs, inbounds, transfers, routes);

        var result = await sut.Handle(new CreateCrossDockCommand(hub.Id, inbound.Id, routeId), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("INBOUND_NOT_ARRIVED");
        transfers.SaveChangesCount.Should().Be(0);
    }

    [Fact]
    public async Task CreateCrossDock_MissingRoute_ReturnsOutboundRouteInvalidAsync()
    {
        var hubs = new InMemoryHubRepository();
        var inbounds = new InMemoryHubInboundRepository();
        var hub = HubEntity.Create("Main Hub", null, null, null, 1000, null);
        var inbound = CreateInbound(hub.Id);
        inbound.ConfirmArrival();
        await hubs.AddAsync(hub, default);
        await inbounds.AddAsync(inbound, default);
        var sut = new CreateCrossDockCommandHandler(
            hubs,
            inbounds,
            new InMemoryCrossDockRepository(),
            new InMemoryDeliveryRouteReader());

        var result = await sut.Handle(
            new CreateCrossDockCommand(hub.Id, inbound.Id, Guid.NewGuid()),
            default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("OUTBOUND_ROUTE_INVALID");
    }

    [Fact]
    public async Task RecordOutbound_EnoughStock_RecordsEventAndAppliesOutboundAsync()
    {
        var hubs = new InMemoryHubRepository();
        var inventory = new InMemoryHubInventoryRepository();
        var outbounds = new InMemoryHubOutboundRepository();
        var routes = new InMemoryDeliveryRouteReader();
        var hub = HubEntity.Create("Main Hub", null, null, null, 1000, null);
        hub.ApplyInbound(20m);
        var marketProductId = Guid.NewGuid();
        var row = HubInventory.Create(hub.Id, marketProductId);
        row.AddInbound(20m);
        var routeId = Guid.NewGuid();
        await hubs.AddAsync(hub, default);
        await inventory.AddAsync(row, default);
        routes.Add(routeId);
        var sut = new RecordOutboundCommandHandler(hubs, inventory, outbounds, routes);

        var result = await sut.Handle(
            new RecordOutboundCommand(
                hub.Id,
                routeId,
                [new HubOutboundItemCommand(marketProductId, null, 7.5m)],
                DateTime.UtcNow),
            default);

        result.IsSuccess.Should().BeTrue();
        row.QuantityOut.Should().Be(7.5m);
        hub.OccupiedCapacityKg.Should().Be(12.5m);
        outbounds.Outbounds.Should().ContainSingle();
        outbounds.SaveChangesCount.Should().Be(1);
    }

    [Fact]
    public async Task RecordOutbound_DuplicateFractionalItems_ChecksGroupedDecimalStockAsync()
    {
        var hubs = new InMemoryHubRepository();
        var inventory = new InMemoryHubInventoryRepository();
        var outbounds = new InMemoryHubOutboundRepository();
        var routes = new InMemoryDeliveryRouteReader();
        var hub = HubEntity.Create("Main Hub", null, null, null, 1000, null);
        hub.ApplyInbound(1m);
        var marketProductId = Guid.NewGuid();
        var row = HubInventory.Create(hub.Id, marketProductId);
        row.AddInbound(1m);
        var routeId = Guid.NewGuid();
        await hubs.AddAsync(hub, default);
        await inventory.AddAsync(row, default);
        routes.Add(routeId);
        var sut = new RecordOutboundCommandHandler(hubs, inventory, outbounds, routes);

        var result = await sut.Handle(
            new RecordOutboundCommand(
                hub.Id,
                routeId,
                [
                    new HubOutboundItemCommand(marketProductId, null, 0.5m),
                    new HubOutboundItemCommand(marketProductId, null, 0.5m)
                ],
                DateTime.UtcNow),
            default);

        result.IsSuccess.Should().BeTrue();
        row.QuantityOut.Should().Be(1m);
        hub.OccupiedCapacityKg.Should().Be(0m);
    }

    [Fact]
    public async Task RecordOutbound_OverDispatch_ReturnsInsufficientStockWithoutMutationAsync()
    {
        var hubs = new InMemoryHubRepository();
        var inventory = new InMemoryHubInventoryRepository();
        var outbounds = new InMemoryHubOutboundRepository();
        var routes = new InMemoryDeliveryRouteReader();
        var hub = HubEntity.Create("Main Hub", null, null, null, 1000, null);
        hub.ApplyInbound(5m);
        var marketProductId = Guid.NewGuid();
        var row = HubInventory.Create(hub.Id, marketProductId);
        row.AddInbound(5m);
        var routeId = Guid.NewGuid();
        await hubs.AddAsync(hub, default);
        await inventory.AddAsync(row, default);
        routes.Add(routeId);
        var sut = new RecordOutboundCommandHandler(hubs, inventory, outbounds, routes);

        var result = await sut.Handle(
            new RecordOutboundCommand(
                hub.Id,
                routeId,
                [new HubOutboundItemCommand(marketProductId, null, 6m)],
                DateTime.UtcNow),
            default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("INSUFFICIENT_HUB_STOCK");
        row.QuantityOut.Should().Be(0m);
        hub.OccupiedCapacityKg.Should().Be(5m);
        outbounds.SaveChangesCount.Should().Be(0);
    }

    [Fact]
    public async Task RecordOutbound_MissingRoute_ReturnsOutboundRouteInvalidAsync()
    {
        var hubs = new InMemoryHubRepository();
        var hub = HubEntity.Create("Main Hub", null, null, null, 1000, null);
        await hubs.AddAsync(hub, default);
        var sut = new RecordOutboundCommandHandler(
            hubs,
            new InMemoryHubInventoryRepository(),
            new InMemoryHubOutboundRepository(),
            new InMemoryDeliveryRouteReader());

        var result = await sut.Handle(
            new RecordOutboundCommand(
                hub.Id,
                Guid.NewGuid(),
                [new HubOutboundItemCommand(Guid.NewGuid(), null, 1m)],
                DateTime.UtcNow),
            default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("OUTBOUND_ROUTE_INVALID");
    }

    [Fact]
    public async Task ListCrossDock_StatusFilter_ReturnsOnlyMatchingHubRowsAsync()
    {
        var hubs = new InMemoryHubRepository();
        var transfers = new InMemoryCrossDockRepository();
        var hub = HubEntity.Create("Main Hub", null, null, null, 1000, null);
        var matching = CrossDockTransfer.Create(hub.Id, Guid.NewGuid(), Guid.NewGuid(), null);
        await hubs.AddAsync(hub, default);
        await transfers.AddAsync(matching, default);
        await transfers.AddAsync(CrossDockTransfer.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null), default);
        var sut = new ListCrossDockQueryHandler(hubs, transfers);

        var result = await sut.Handle(
            new ListCrossDockQuery(hub.Id, CrossDockTransfer.StatusPending),
            default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().ContainSingle()
            .Which.CrossDockId.Should().Be(matching.Id);
    }

    [Fact]
    public async Task ListOutbound_DateFilter_ReturnsDateTotalsAsync()
    {
        var hubs = new InMemoryHubRepository();
        var outbounds = new InMemoryHubOutboundRepository();
        var hub = HubEntity.Create("Main Hub", null, null, null, 1000, null);
        var marketProductId = Guid.NewGuid();
        var date = new DateOnly(2026, 7, 11);
        await hubs.AddAsync(hub, default);
        await outbounds.AddAsync(
            HubOutboundEvent.Record(
                hub.Id,
                Guid.NewGuid(),
                [new HubOutboundItem(marketProductId, null, 2m)],
                date.ToDateTime(TimeOnly.MinValue)),
            default);
        await outbounds.AddAsync(
            HubOutboundEvent.Record(
                hub.Id,
                Guid.NewGuid(),
                [new HubOutboundItem(marketProductId, null, 3m)],
                date.AddDays(-1).ToDateTime(TimeOnly.MinValue)),
            default);
        var sut = new ListOutboundQueryHandler(hubs, outbounds);

        var result = await sut.Handle(new ListOutboundQuery(hub.Id, date), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().ContainSingle();
        result.Value.TotalQuantityKg.Should().Be(2m);
    }

    private static HubInboundEvent CreateInbound(Guid hubId) =>
        HubInboundEvent.Record(
            hubId,
            null,
            null,
            null,
            [new HubInboundItem(Guid.NewGuid(), null, 1m)],
            DateTime.UtcNow);
}
