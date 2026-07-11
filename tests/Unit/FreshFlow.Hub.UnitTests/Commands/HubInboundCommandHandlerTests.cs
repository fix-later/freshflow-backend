using FluentAssertions;
using FreshFlow.Hub.Application.Commands.RecordInbound;
using FreshFlow.Hub.Application.Commands.ScanInbound;
using FreshFlow.Hub.Domain.Entities;
using FreshFlow.Hub.UnitTests.TestDoubles;
using HubEntity = FreshFlow.Hub.Domain.Entities.Hub;

namespace FreshFlow.Hub.UnitTests.Commands;

[Trait("Category", "Unit")]
public sealed class HubInboundCommandHandlerTests
{
    [Fact]
    public async Task RecordInbound_ExistingHub_CreatesPendingEventWithoutChangingOccupiedCapacityAsync()
    {
        var hubs = new InMemoryHubRepository();
        var inbounds = new InMemoryHubInboundRepository();
        var hub = HubEntity.Create("Main Hub", null, null, null, 1000, null);
        await hubs.AddAsync(hub, default);
        var sut = new RecordInboundCommandHandler(hubs, inbounds);

        var result = await sut.Handle(
            new RecordInboundCommand(
                hub.Id,
                Guid.NewGuid(),
                Guid.NewGuid(),
                [new HubInboundItemCommand(Guid.NewGuid(), Guid.NewGuid(), 25m)],
                DateTime.UtcNow),
            default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(HubInboundEvent.StatusPending);
        result.Value.TotalQuantityKg.Should().Be(25m);
        hub.OccupiedCapacityKg.Should().Be(0);
        inbounds.Inbounds.Should().ContainSingle();
        inbounds.SaveChangesCount.Should().Be(1);
    }

    [Fact]
    public async Task RecordInbound_SaveConcurrencyConflict_ReturnsAlreadyReceivedAsync()
    {
        var hubs = new InMemoryHubRepository();
        var inbounds = new InMemoryHubInboundRepository { ThrowConcurrencyOnSave = true };
        var hub = HubEntity.Create("Main Hub", null, null, null, 1000, null);
        await hubs.AddAsync(hub, default);
        var sut = new RecordInboundCommandHandler(hubs, inbounds);

        var result = await sut.Handle(
            new RecordInboundCommand(
                hub.Id,
                null,
                Guid.NewGuid(),
                [new HubInboundItemCommand(Guid.NewGuid(), null, 5m)],
                DateTime.UtcNow),
            default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ALREADY_RECEIVED");
    }

    [Fact]
    public async Task RecordInbound_DuplicateDeliverySchedule_ReturnsAlreadyReceivedAsync()
    {
        var hubs = new InMemoryHubRepository();
        var inbounds = new InMemoryHubInboundRepository();
        var hub = HubEntity.Create("Main Hub", null, null, null, 1000, null);
        var deliveryScheduleId = Guid.NewGuid();
        await hubs.AddAsync(hub, default);
        await inbounds.AddAsync(CreateInbound(hub.Id, deliveryScheduleId, 10m), default);
        var sut = new RecordInboundCommandHandler(hubs, inbounds);

        var result = await sut.Handle(
            new RecordInboundCommand(
                hub.Id,
                null,
                deliveryScheduleId,
                [new HubInboundItemCommand(Guid.NewGuid(), null, 5m)],
                DateTime.UtcNow),
            default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ALREADY_RECEIVED");
        inbounds.SaveChangesCount.Should().Be(0);
    }

    [Fact]
    public async Task RecordInbound_MissingHub_ReturnsHubNotFoundAsync()
    {
        var sut = new RecordInboundCommandHandler(
            new InMemoryHubRepository(),
            new InMemoryHubInboundRepository());

        var result = await sut.Handle(
            new RecordInboundCommand(
                Guid.NewGuid(),
                null,
                null,
                [new HubInboundItemCommand(Guid.NewGuid(), null, 5m)],
                DateTime.UtcNow),
            default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("HUB_NOT_FOUND");
    }

    [Fact]
    public async Task ScanInbound_ValidPendingEvent_ConfirmsArrivalUpsertsInventoryAndAppliesCapacityAsync()
    {
        var hubs = new InMemoryHubRepository();
        var inbounds = new InMemoryHubInboundRepository();
        var inventory = new InMemoryHubInventoryRepository();
        var hub = HubEntity.Create("Main Hub", null, null, null, 1000, null);
        var marketProductId = Guid.NewGuid();
        var inbound = HubInboundEvent.Record(
            hub.Id,
            null,
            null,
            null,
            [new HubInboundItem(marketProductId, Guid.NewGuid(), 40m)],
            DateTime.UtcNow);
        await hubs.AddAsync(hub, default);
        await inbounds.AddAsync(inbound, default);
        var sut = new ScanInboundCommandHandler(hubs, inbounds, inventory);

        var result = await sut.Handle(new ScanInboundCommand(inbound.Id.ToString()), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(HubInboundEvent.StatusArrivedAtHub);
        hub.OccupiedCapacityKg.Should().Be(40m);
        inventory.Inventory.Should().ContainSingle(i =>
            i.HubId == hub.Id &&
            i.MarketProductId == marketProductId &&
            i.QuantityIn == 40m);
        inbounds.SaveChangesCount.Should().Be(1);
    }

    [Fact]
    public async Task ScanInbound_ExistingInventory_AddsQuantityInAsync()
    {
        var hubs = new InMemoryHubRepository();
        var inbounds = new InMemoryHubInboundRepository();
        var inventory = new InMemoryHubInventoryRepository();
        var hub = HubEntity.Create("Main Hub", null, null, null, 1000, null);
        var marketProductId = Guid.NewGuid();
        var existing = HubInventory.Create(hub.Id, marketProductId);
        existing.AddInbound(10m);
        var inbound = HubInboundEvent.Record(
            hub.Id,
            null,
            null,
            null,
            [new HubInboundItem(marketProductId, null, 15m)],
            DateTime.UtcNow);
        await hubs.AddAsync(hub, default);
        await inventory.AddAsync(existing, default);
        await inbounds.AddAsync(inbound, default);
        var sut = new ScanInboundCommandHandler(hubs, inbounds, inventory);

        var result = await sut.Handle(new ScanInboundCommand(inbound.Id.ToString()), default);

        result.IsSuccess.Should().BeTrue();
        inventory.Inventory.Should().ContainSingle();
        existing.QuantityIn.Should().Be(25m);
    }

    [Fact]
    public async Task ScanInbound_SameProductFractionalItems_PreservesDecimalQuantityAsync()
    {
        var hubs = new InMemoryHubRepository();
        var inbounds = new InMemoryHubInboundRepository();
        var inventory = new InMemoryHubInventoryRepository();
        var hub = HubEntity.Create("Main Hub", null, null, null, 1000, null);
        var marketProductId = Guid.NewGuid();
        var inbound = CreateInbound(
            hub.Id,
            null,
            [
                new HubInboundItem(marketProductId, null, 0.4m),
                new HubInboundItem(marketProductId, null, 0.4m)
            ]);
        await hubs.AddAsync(hub, default);
        await inbounds.AddAsync(inbound, default);
        var sut = new ScanInboundCommandHandler(hubs, inbounds, inventory);

        var result = await sut.Handle(new ScanInboundCommand(inbound.Id.ToString()), default);

        result.IsSuccess.Should().BeTrue();
        inventory.Inventory.Should().ContainSingle(i =>
            i.HubId == hub.Id &&
            i.MarketProductId == marketProductId &&
            i.QuantityIn == 0.8m);
        hub.OccupiedCapacityKg.Should().Be(0.8m);
    }

    [Fact]
    public async Task ScanInbound_RepeatedFractionalEvents_AccumulatesDecimalQuantityWithoutRoundingDriftAsync()
    {
        var hubs = new InMemoryHubRepository();
        var inbounds = new InMemoryHubInboundRepository();
        var inventory = new InMemoryHubInventoryRepository();
        var hub = HubEntity.Create("Main Hub", null, null, null, 1000, null);
        var marketProductId = Guid.NewGuid();
        var events = Enumerable
            .Range(0, 10)
            .Select(_ => CreateInbound(
                hub.Id,
                null,
                [new HubInboundItem(marketProductId, null, 0.6m)]))
            .ToList();
        await hubs.AddAsync(hub, default);
        foreach (var inbound in events)
            await inbounds.AddAsync(inbound, default);
        var sut = new ScanInboundCommandHandler(hubs, inbounds, inventory);

        foreach (var inbound in events)
        {
            var result = await sut.Handle(new ScanInboundCommand(inbound.Id.ToString()), default);

            result.IsSuccess.Should().BeTrue();
        }

        inventory.Inventory.Should().ContainSingle(i =>
            i.HubId == hub.Id &&
            i.MarketProductId == marketProductId &&
            i.QuantityIn == 6.0m);
        hub.OccupiedCapacityKg.Should().Be(6.0m);
        inbounds.SaveChangesCount.Should().Be(10);
    }

    [Fact]
    public async Task ScanInbound_CapacityExceeded_Returns422ErrorAndDoesNotMutateAsync()
    {
        var hubs = new InMemoryHubRepository();
        var inbounds = new InMemoryHubInboundRepository();
        var inventory = new InMemoryHubInventoryRepository();
        var hub = HubEntity.Create("Small Hub", null, null, null, 10, null);
        var inbound = CreateInbound(hub.Id, null, 25m);
        await hubs.AddAsync(hub, default);
        await inbounds.AddAsync(inbound, default);
        var sut = new ScanInboundCommandHandler(hubs, inbounds, inventory);

        var result = await sut.Handle(new ScanInboundCommand(inbound.Id.ToString()), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("HUB_CAPACITY_EXCEEDED");
        inbound.Status.Should().Be(HubInboundEvent.StatusPending);
        hub.OccupiedCapacityKg.Should().Be(0);
        inventory.Inventory.Should().BeEmpty();
        inbounds.SaveChangesCount.Should().Be(0);
    }

    [Theory]
    [InlineData("not-a-guid")]
    public async Task ScanInbound_InvalidCode_ReturnsScanNoMatchAsync(string code)
    {
        var sut = new ScanInboundCommandHandler(
            new InMemoryHubRepository(),
            new InMemoryHubInboundRepository(),
            new InMemoryHubInventoryRepository());

        var result = await sut.Handle(new ScanInboundCommand(code), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("SCAN_NO_MATCH");
    }

    [Fact]
    public async Task ScanInbound_NonPendingEvent_ReturnsScanNoMatchAsync()
    {
        var hubs = new InMemoryHubRepository();
        var inbounds = new InMemoryHubInboundRepository();
        var inbound = CreateInbound(Guid.NewGuid(), null, 10m);
        inbound.ConfirmArrival();
        await inbounds.AddAsync(inbound, default);
        var sut = new ScanInboundCommandHandler(hubs, inbounds, new InMemoryHubInventoryRepository());

        var result = await sut.Handle(new ScanInboundCommand(inbound.Id.ToString()), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("SCAN_NO_MATCH");
    }

    private static HubInboundEvent CreateInbound(Guid hubId, Guid? deliveryScheduleId, decimal quantityKg) =>
        CreateInbound(
            hubId,
            deliveryScheduleId,
            [new HubInboundItem(Guid.NewGuid(), null, quantityKg)]);

    private static HubInboundEvent CreateInbound(
        Guid hubId,
        Guid? deliveryScheduleId,
        IReadOnlyList<HubInboundItem> items) =>
        HubInboundEvent.Record(
            hubId,
            null,
            null,
            deliveryScheduleId,
            items,
            DateTime.UtcNow);
}
