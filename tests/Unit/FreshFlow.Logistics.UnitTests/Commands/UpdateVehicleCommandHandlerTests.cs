using FluentAssertions;
using FreshFlow.Logistics.Application.Commands.UpdateVehicle;
using FreshFlow.Logistics.Domain.Entities;
using FreshFlow.Logistics.Domain.Enums;
using FreshFlow.Logistics.UnitTests.TestDoubles;

namespace FreshFlow.Logistics.UnitTests.Commands;

[Trait("Category", "Unit")]
public sealed class UpdateVehicleCommandHandlerTests
{
    [Fact]
    public async Task Handle_ExistingVehicle_UpdatesFieldsAsync()
    {
        var repository = new InMemoryVehicleRepository();
        var vehicle = new Vehicle("ABC-123", 1200, VehicleType.van, null);
        await repository.AddAsync(vehicle, default);
        var sut = new UpdateVehicleCommandHandler(repository);

        var result = await sut.Handle(
            new UpdateVehicleCommand(vehicle.Id, "XYZ-789", 2400, "truck"),
            default);

        result.IsSuccess.Should().BeTrue();
        result.Value.PlateNumber.Should().Be("XYZ-789");
        result.Value.CapacityKg.Should().Be(2400);
        result.Value.VehicleType.Should().Be("truck");
        vehicle.PlateNumber.Should().Be("XYZ-789");
        repository.SaveChangesCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_MissingVehicle_ReturnsNotFoundAsync()
    {
        var repository = new InMemoryVehicleRepository();
        var sut = new UpdateVehicleCommandHandler(repository);
        var id = Guid.NewGuid();

        var result = await sut.Handle(new UpdateVehicleCommand(id, "ABC-123", 1200, "van"), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("VEHICLE_NOT_FOUND");
        repository.SaveChangesCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_PlateNumberUsedByDifferentVehicle_ReturnsDuplicateAsync()
    {
        var repository = new InMemoryVehicleRepository();
        var first = new Vehicle("ABC-123", 1200, VehicleType.van, null);
        var second = new Vehicle("XYZ-789", 1200, VehicleType.truck, null);
        await repository.AddAsync(first, default);
        await repository.AddAsync(second, default);
        var sut = new UpdateVehicleCommandHandler(repository);

        var result = await sut.Handle(
            new UpdateVehicleCommand(second.Id, "ABC-123", 1500, "truck"),
            default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("PLATE_NUMBER_DUPLICATE");
        second.PlateNumber.Should().Be("XYZ-789");
        repository.SaveChangesCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_InvalidVehicleType_ReturnsValidationFailureAsync()
    {
        var repository = new InMemoryVehicleRepository();
        var vehicle = new Vehicle("ABC-123", 1200, VehicleType.van, null);
        await repository.AddAsync(vehicle, default);
        var sut = new UpdateVehicleCommandHandler(repository);

        var result = await sut.Handle(
            new UpdateVehicleCommand(vehicle.Id, "ABC-123", 1500, "boat"),
            default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("VALIDATION_ERROR");
        vehicle.VehicleType.Should().Be(VehicleType.van);
        repository.SaveChangesCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_SamePlateNumber_DoesNotTreatAsDuplicateAsync()
    {
        var repository = new InMemoryVehicleRepository();
        var vehicle = new Vehicle("ABC-123", 1200, VehicleType.van, null);
        await repository.AddAsync(vehicle, default);
        var sut = new UpdateVehicleCommandHandler(repository);

        var result = await sut.Handle(
            new UpdateVehicleCommand(vehicle.Id, "ABC-123", 1500, "truck"),
            default);

        result.IsSuccess.Should().BeTrue();
        result.Value.PlateNumber.Should().Be("ABC-123");
        repository.SaveChangesCount.Should().Be(1);
    }
}
