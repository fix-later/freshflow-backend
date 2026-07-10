using FluentAssertions;
using FreshFlow.Hub.Application.Commands.CreateHandover;
using FreshFlow.Hub.Application.Commands.DriverCheckout;
using FreshFlow.Hub.Application.Queries.ListHandovers;
using FreshFlow.Hub.Domain.Entities;
using FreshFlow.Hub.UnitTests.TestDoubles;
using HubEntity = FreshFlow.Hub.Domain.Entities.Hub;

namespace FreshFlow.Hub.UnitTests.Commands;

[Trait("Category", "Unit")]
public sealed class HubHandoverCommandHandlerTests
{
    [Fact]
    public async Task CreateHandover_ExistingAssignedRoute_CreatesPendingHandoverAsync()
    {
        var hubs = new InMemoryHubRepository();
        var routes = new InMemoryDeliveryRouteReader();
        var handovers = new InMemoryHubHandoverRepository();
        var hub = HubEntity.Create("Main Hub", null, null, null, 1000, null);
        var routeId = Guid.NewGuid();
        var driverUserId = Guid.NewGuid();
        await hubs.AddAsync(hub, default);
        routes.Add(routeId, driverUserId);
        var sut = new CreateHandoverCommandHandler(hubs, routes, handovers);

        var result = await sut.Handle(
            new CreateHandoverCommand(hub.Id, routeId, driverUserId, null, Guid.NewGuid(), "Ready"),
            default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(HubHandoverEvent.StatusPendingCheckout);
        handovers.Handovers.Should().ContainSingle();
        handovers.SaveChangesCount.Should().Be(1);
    }

    [Fact]
    public async Task CreateHandover_MissingRoute_ReturnsNotFoundAsync()
    {
        var hubs = new InMemoryHubRepository();
        var hub = HubEntity.Create("Main Hub", null, null, null, 1000, null);
        await hubs.AddAsync(hub, default);
        var sut = new CreateHandoverCommandHandler(
            hubs,
            new InMemoryDeliveryRouteReader(),
            new InMemoryHubHandoverRepository());

        var result = await sut.Handle(
            new CreateHandoverCommand(hub.Id, Guid.NewGuid(), Guid.NewGuid(), null, Guid.NewGuid()),
            default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("DELIVERY_ROUTE_NOT_FOUND");
    }

    [Fact]
    public async Task CreateHandover_RouteWithoutDriver_ReturnsRouteHasNoDriverAsync()
    {
        var hubs = new InMemoryHubRepository();
        var routes = new InMemoryDeliveryRouteReader();
        var hub = HubEntity.Create("Main Hub", null, null, null, 1000, null);
        var routeId = Guid.NewGuid();
        await hubs.AddAsync(hub, default);
        routes.Add(routeId);
        var sut = new CreateHandoverCommandHandler(hubs, routes, new InMemoryHubHandoverRepository());

        var result = await sut.Handle(
            new CreateHandoverCommand(hub.Id, routeId, Guid.NewGuid(), null, Guid.NewGuid()),
            default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ROUTE_HAS_NO_DRIVER");
    }

    [Fact]
    public async Task CreateHandover_DriverMismatch_ReturnsDriverRouteMismatchAsync()
    {
        var hubs = new InMemoryHubRepository();
        var routes = new InMemoryDeliveryRouteReader();
        var hub = HubEntity.Create("Main Hub", null, null, null, 1000, null);
        var routeId = Guid.NewGuid();
        await hubs.AddAsync(hub, default);
        routes.Add(routeId, Guid.NewGuid());
        var sut = new CreateHandoverCommandHandler(hubs, routes, new InMemoryHubHandoverRepository());

        var result = await sut.Handle(
            new CreateHandoverCommand(hub.Id, routeId, Guid.NewGuid(), null, Guid.NewGuid()),
            default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("DRIVER_ROUTE_MISMATCH");
    }

    [Fact]
    public async Task DriverCheckout_AssignedDriver_MarksCheckedOutAsync()
    {
        var handovers = new InMemoryHubHandoverRepository();
        var driverUserId = Guid.NewGuid();
        var handover = CreateHandover(driverUserId: driverUserId);
        await handovers.AddAsync(handover, default);
        var sut = new DriverCheckoutCommandHandler(handovers);

        var result = await sut.Handle(new DriverCheckoutCommand(handover.HubId, handover.Id, driverUserId), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(HubHandoverEvent.StatusCheckedOut);
        handover.Status.Should().Be(HubHandoverEvent.StatusCheckedOut);
        handovers.SaveChangesCount.Should().Be(1);
    }

    [Fact]
    public async Task DriverCheckout_DifferentDriver_ReturnsForbiddenAsync()
    {
        var handovers = new InMemoryHubHandoverRepository();
        var handover = CreateHandover();
        await handovers.AddAsync(handover, default);
        var sut = new DriverCheckoutCommandHandler(handovers);

        var result = await sut.Handle(new DriverCheckoutCommand(handover.HubId, handover.Id, Guid.NewGuid()), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("FORBIDDEN");
        handovers.SaveChangesCount.Should().Be(0);
    }

    [Fact]
    public async Task DriverCheckout_AlreadyCheckedOut_ReturnsConflictAsync()
    {
        var handovers = new InMemoryHubHandoverRepository();
        var driverUserId = Guid.NewGuid();
        var handover = CreateHandover(driverUserId: driverUserId);
        handover.ConfirmCheckout(driverUserId);
        await handovers.AddAsync(handover, default);
        var sut = new DriverCheckoutCommandHandler(handovers);

        var result = await sut.Handle(new DriverCheckoutCommand(handover.HubId, handover.Id, driverUserId), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("HUB_HANDOVER_ALREADY_CHECKED_OUT");
        handovers.SaveChangesCount.Should().Be(0);
    }

    [Fact]
    public async Task ListHandovers_ReturnsHubRowsAsync()
    {
        var hubs = new InMemoryHubRepository();
        var handovers = new InMemoryHubHandoverRepository();
        var hub = HubEntity.Create("Main Hub", null, null, null, 1000, null);
        var matching = CreateHandover(hub.Id);
        await hubs.AddAsync(hub, default);
        await handovers.AddAsync(matching, default);
        await handovers.AddAsync(CreateHandover(Guid.NewGuid()), default);
        var sut = new ListHandoversQueryHandler(hubs, handovers);

        var result = await sut.Handle(new ListHandoversQuery(hub.Id), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().ContainSingle()
            .Which.HandoverId.Should().Be(matching.Id);
    }

    private static HubHandoverEvent CreateHandover(Guid? hubId = null, Guid? driverUserId = null) =>
        HubHandoverEvent.Create(
            hubId ?? Guid.NewGuid(),
            Guid.NewGuid(),
            driverUserId ?? Guid.NewGuid(),
            null,
            Guid.NewGuid(),
            null);
}
