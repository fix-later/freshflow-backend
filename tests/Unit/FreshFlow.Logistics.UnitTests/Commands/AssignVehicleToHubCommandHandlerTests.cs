using FluentAssertions;
using FreshFlow.Logistics.Application.Abstractions;
using FreshFlow.Logistics.Application.Commands.AssignVehicleToHub;
using FreshFlow.Logistics.Domain.Entities;
using FreshFlow.Logistics.Domain.Enums;
using FreshFlow.Logistics.UnitTests.TestDoubles;
using NSubstitute;

namespace FreshFlow.Logistics.UnitTests.Commands;

[Trait("Category", "Unit")]
public sealed class AssignVehicleToHubCommandHandlerTests
{
    [Fact]
    public async Task Handle_ExistingVehicleAndHub_AssignsHubAsync()
    {
        var vehicles = new InMemoryVehicleRepository();
        var vehicle = new Vehicle("ABC-123", 1200, VehicleType.van, null);
        await vehicles.AddAsync(vehicle, default);
        var hubs = Substitute.For<IHubCoordinateReader>();
        var hubId = Guid.NewGuid();
        hubs.FindByIdAsync(hubId, Arg.Any<CancellationToken>())
            .Returns(new HubCoordinateDto(hubId, Guid.NewGuid(), "Hub", 10m, 106m));
        var sut = new AssignVehicleToHubCommandHandler(vehicles, hubs);

        var result = await sut.Handle(new AssignVehicleToHubCommand(vehicle.Id, hubId), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.HubId.Should().Be(hubId);
        vehicle.HubId.Should().Be(hubId);
        vehicles.SaveChangesCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_HubMissing_ReturnsHubNotFoundAsync()
    {
        var vehicles = new InMemoryVehicleRepository();
        var vehicle = new Vehicle("ABC-123", 1200, VehicleType.van, null);
        await vehicles.AddAsync(vehicle, default);
        var hubs = Substitute.For<IHubCoordinateReader>();
        var sut = new AssignVehicleToHubCommandHandler(vehicles, hubs);

        var result = await sut.Handle(
            new AssignVehicleToHubCommand(vehicle.Id, Guid.NewGuid()), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("HUB_NOT_FOUND");
        vehicles.SaveChangesCount.Should().Be(0);
    }
}
