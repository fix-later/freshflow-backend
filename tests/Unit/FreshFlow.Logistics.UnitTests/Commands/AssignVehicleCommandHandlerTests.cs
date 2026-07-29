using FluentAssertions;
using FreshFlow.Logistics.Application.Commands.AssignVehicle;
using FreshFlow.Logistics.Application.Dtos;
using FreshFlow.Logistics.Application.Queries.CheckEligibility;
using FreshFlow.Logistics.Domain.Entities;
using FreshFlow.Logistics.Domain.Enums;
using FreshFlow.Logistics.Domain.ValueObjects;
using FreshFlow.Logistics.UnitTests.TestDoubles;
using FreshFlow.SharedKernel.Application;
using MediatR;
using NSubstitute;

namespace FreshFlow.Logistics.UnitTests.Commands;

[Trait("Category", "Unit")]
public sealed class AssignVehicleCommandHandlerTests
{
    [Fact]
    public async Task Handle_ReviewedRouteAndEligible_AssignsVehicleAndDriverAsync()
    {
        var repository = new InMemoryDeliveryRouteRepository();
        var route = ReviewedRoute();
        await repository.AddAsync(route, default);
        var sender = EligibleSender();
        var vehicleId = Guid.NewGuid();
        var driverUserId = Guid.NewGuid();
        var sut = new AssignVehicleCommandHandler(repository, sender);

        var result = await sut.Handle(new AssignVehicleCommand(route.Id, vehicleId, driverUserId), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("assigned");
        result.Value.VehicleId.Should().Be(vehicleId);
        result.Value.DriverUserId.Should().Be(driverUserId);
        route.VehicleId.Should().Be(vehicleId);
        route.DriverUserId.Should().Be(driverUserId);
        repository.SaveAssignmentCount.Should().Be(1);
        await sender.Received(1).Send(
            Arg.Is<CheckEligibilityQuery>(query =>
                query.RouteId == route.Id &&
                query.VehicleId == vehicleId &&
                query.DriverUserId == driverUserId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_RouteMissing_ReturnsNotFoundAndSkipsEligibilityAsync()
    {
        var repository = new InMemoryDeliveryRouteRepository();
        var sender = Substitute.For<ISender>();
        var routeId = Guid.NewGuid();
        var sut = new AssignVehicleCommandHandler(repository, sender);

        var result = await sut.Handle(new AssignVehicleCommand(routeId, Guid.NewGuid(), null), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("DELIVERY_ROUTE_NOT_FOUND");
        repository.SaveAssignmentCount.Should().Be(0);
        await sender.DidNotReceiveWithAnyArgs().Send(Arg.Any<CheckEligibilityQuery>(), default);
    }

    [Fact]
    public async Task Handle_EligibilityFailure_PropagatesErrorAsync()
    {
        var repository = new InMemoryDeliveryRouteRepository();
        var route = ReviewedRoute();
        await repository.AddAsync(route, default);
        var sender = Substitute.For<ISender>();
        sender.Send(Arg.Any<CheckEligibilityQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<EligibilityResultDto>.Failure(Error.NotFound("DELIVERY_ROUTE", route.Id)));
        var sut = new AssignVehicleCommandHandler(repository, sender);

        var result = await sut.Handle(
            new AssignVehicleCommand(route.Id, Guid.NewGuid(), Guid.NewGuid()), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("DELIVERY_ROUTE_NOT_FOUND");
        repository.SaveAssignmentCount.Should().Be(0);
    }

    [Theory]
    [InlineData("VEHICLE_CAPACITY_EXCEEDED")]
    [InlineData("DRIVER_NOT_FOUND")]
    public async Task Handle_NotEligibleInputReasons_ReturnsVehicleNotEligibleAsync(string reason)
    {
        var repository = new InMemoryDeliveryRouteRepository();
        var route = ReviewedRoute();
        await repository.AddAsync(route, default);
        var sender = EligibilitySender(false, [reason]);
        var sut = new AssignVehicleCommandHandler(repository, sender);

        var result = await sut.Handle(
            new AssignVehicleCommand(route.Id, Guid.NewGuid(), Guid.NewGuid()), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("VEHICLE_NOT_ELIGIBLE");
        repository.SaveAssignmentCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WeightCapacityExceeded_ReturnsValidationErrorAsync()
    {
        var repository = new InMemoryDeliveryRouteRepository();
        var route = ReviewedRoute();
        await repository.AddAsync(route, default);
        var sender = EligibilitySender(false, ["VEHICLE_WEIGHT_CAPACITY_EXCEEDED"]);
        var sut = new AssignVehicleCommandHandler(repository, sender);

        var result = await sut.Handle(
            new AssignVehicleCommand(route.Id, Guid.NewGuid(), null), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("VALIDATION_ERROR");
        result.Error.Message.Should().Contain("VEHICLE_WEIGHT_CAPACITY_EXCEEDED");
        repository.SaveAssignmentCount.Should().Be(0);
    }

    [Theory]
    [InlineData("VEHICLE_DOUBLE_BOOKED")]
    [InlineData("VEHICLE_UNAVAILABLE")]
    public async Task Handle_NotEligibleConflictReasons_ReturnsVehicleNotAvailableAsync(string reason)
    {
        var repository = new InMemoryDeliveryRouteRepository();
        var route = ReviewedRoute();
        await repository.AddAsync(route, default);
        var sender = EligibilitySender(false, [reason]);
        var sut = new AssignVehicleCommandHandler(repository, sender);

        var result = await sut.Handle(new AssignVehicleCommand(route.Id, Guid.NewGuid(), null), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("VEHICLE_NOT_AVAILABLE");
        repository.SaveAssignmentCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_RouteNotReviewed_ReturnsInvalidTransitionAsync()
    {
        var repository = new InMemoryDeliveryRouteRepository();
        var route = SelectedRoute();
        await repository.AddAsync(route, default);
        var sender = EligibleSender();
        var sut = new AssignVehicleCommandHandler(repository, sender);

        var result = await sut.Handle(
            new AssignVehicleCommand(route.Id, Guid.NewGuid(), Guid.NewGuid()), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ROUTE_INVALID_TRANSITION");
        repository.SaveAssignmentCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_AlreadyAssignedDifferentVehicle_ReturnsInvalidTransitionAsync()
    {
        var repository = new InMemoryDeliveryRouteRepository();
        var route = ReviewedRoute();
        route.Assign(Guid.NewGuid(), Guid.NewGuid());
        await repository.AddAsync(route, default);
        var sender = EligibleSender();
        var sut = new AssignVehicleCommandHandler(repository, sender);

        var result = await sut.Handle(new AssignVehicleCommand(route.Id, Guid.NewGuid(), route.DriverUserId), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ROUTE_INVALID_TRANSITION");
        repository.SaveAssignmentCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_IdempotentReassignSameVehicleAndDriver_ReturnsSuccessAsync()
    {
        var repository = new InMemoryDeliveryRouteRepository();
        var route = ReviewedRoute();
        var vehicleId = Guid.NewGuid();
        var driverUserId = Guid.NewGuid();
        route.Assign(vehicleId, driverUserId);
        var updatedAt = route.UpdatedAt;
        await repository.AddAsync(route, default);
        var sender = EligibleSender();
        var sut = new AssignVehicleCommandHandler(repository, sender);

        var result = await sut.Handle(new AssignVehicleCommand(route.Id, vehicleId, driverUserId), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("assigned");
        result.Value.VehicleId.Should().Be(vehicleId);
        result.Value.DriverUserId.Should().Be(driverUserId);
        route.UpdatedAt.Should().Be(updatedAt);
        repository.SaveAssignmentCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_SaveAssignmentConflict_ReturnsVehicleNotAvailableAsync()
    {
        var repository = new InMemoryDeliveryRouteRepository { SaveAssignmentResult = false };
        var route = ReviewedRoute();
        await repository.AddAsync(route, default);
        var sender = EligibleSender();
        var sut = new AssignVehicleCommandHandler(repository, sender);

        var result = await sut.Handle(
            new AssignVehicleCommand(route.Id, Guid.NewGuid(), Guid.NewGuid()), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("VEHICLE_NOT_AVAILABLE");
        repository.SaveAssignmentCount.Should().Be(1);
    }

    private static ISender EligibleSender() => EligibilitySender(true, []);

    private static ISender EligibilitySender(bool isEligible, IReadOnlyList<string> reasons)
    {
        var sender = Substitute.For<ISender>();
        sender.Send(Arg.Any<CheckEligibilityQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<EligibilityResultDto>.Success(new EligibilityResultDto(isEligible, reasons)));
        return sender;
    }

    private static DeliveryRoute ReviewedRoute()
    {
        var route = SelectedRoute();
        route.ApplyOptimization(route.Stops, 12.34m, 25, 61700m, OptimizationCriteria.cost);
        route.MarkReviewed();
        return route;
    }

    private static DeliveryRoute SelectedRoute()
    {
        var route = DeliveryRoute.CreateDirect(
            new DateOnly(2026, 7, 9),
            [
                new RouteStop(0, StopEntityType.market, Guid.NewGuid(), "Market", 10.1m, 106.1m, null, null),
                new RouteStop(1, StopEntityType.restaurant, Guid.NewGuid(), "Restaurant", 10.2m, 106.2m, null, null)
            ],
            null);
        route.Select();
        return route;
    }
}
