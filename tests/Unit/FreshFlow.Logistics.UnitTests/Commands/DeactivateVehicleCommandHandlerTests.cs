using FluentAssertions;
using FreshFlow.Logistics.Application.Commands.DeactivateVehicle;
using FreshFlow.Logistics.Domain.Entities;
using FreshFlow.Logistics.Domain.Enums;
using FreshFlow.Logistics.UnitTests.TestDoubles;

namespace FreshFlow.Logistics.UnitTests.Commands;

[Trait("Category", "Unit")]
public sealed class DeactivateVehicleCommandHandlerTests
{
    [Fact]
    public async Task Handle_ExistingVehicle_SetsDeletedAtAsync()
    {
        var repository = new InMemoryVehicleRepository();
        var vehicle = new Vehicle("ABC-123", 1200, VehicleType.van, null);
        await repository.AddAsync(vehicle, default);
        var sut = new DeactivateVehicleCommandHandler(repository);

        var result = await sut.Handle(new DeactivateVehicleCommand(vehicle.Id), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.IsActive.Should().BeFalse();
        vehicle.DeletedAt.Should().NotBeNull();
        repository.SaveChangesCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_AlreadyDeactivated_RemainsSuccessfulAndKeepsDeletedAtAsync()
    {
        var repository = new InMemoryVehicleRepository();
        var vehicle = new Vehicle("ABC-123", 1200, VehicleType.van, null);
        vehicle.Deactivate();
        var firstDeletedAt = vehicle.DeletedAt;
        await repository.AddAsync(vehicle, default);
        var sut = new DeactivateVehicleCommandHandler(repository);

        var result = await sut.Handle(new DeactivateVehicleCommand(vehicle.Id), default);

        result.IsSuccess.Should().BeTrue();
        vehicle.DeletedAt.Should().Be(firstDeletedAt);
        repository.SaveChangesCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_MissingVehicle_ReturnsNotFoundAsync()
    {
        var repository = new InMemoryVehicleRepository();
        var sut = new DeactivateVehicleCommandHandler(repository);

        var result = await sut.Handle(new DeactivateVehicleCommand(Guid.NewGuid()), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("VEHICLE_NOT_FOUND");
        repository.SaveChangesCount.Should().Be(0);
    }
}
