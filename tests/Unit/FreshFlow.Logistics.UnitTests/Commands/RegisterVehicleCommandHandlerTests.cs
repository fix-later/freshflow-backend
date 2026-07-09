using FluentAssertions;
using FreshFlow.Logistics.Application.Commands.RegisterVehicle;
using FreshFlow.Logistics.Domain.Entities;
using FreshFlow.Logistics.Domain.Enums;
using FreshFlow.Logistics.UnitTests.TestDoubles;

namespace FreshFlow.Logistics.UnitTests.Commands;

[Trait("Category", "Unit")]
public sealed class RegisterVehicleCommandHandlerTests
{
    [Fact]
    public async Task Handle_NewVehicle_CreatesAvailableActiveVehicleAsync()
    {
        var repository = new InMemoryVehicleRepository();
        var sut = new RegisterVehicleCommandHandler(repository);
        var registeredBy = Guid.NewGuid();

        var result = await sut.Handle(
            new RegisterVehicleCommand("ABC-123", 1200, "van", registeredBy),
            default);

        result.IsSuccess.Should().BeTrue();
        result.Value.PlateNumber.Should().Be("ABC-123");
        result.Value.VehicleType.Should().Be("van");
        result.Value.IsAvailable.Should().BeTrue();
        result.Value.IsActive.Should().BeTrue();
        repository.Vehicles.Should().ContainSingle();
        repository.Vehicles.Single().RegisteredBy.Should().Be(registeredBy);
        repository.Vehicles.Single().DeletedAt.Should().BeNull();
        repository.SaveChangesCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_DuplicatePlateNumber_ReturnsFailureAndDoesNotCreateRecordAsync()
    {
        var repository = new InMemoryVehicleRepository();
        var existing = new Vehicle("ABC-123", 1200, VehicleType.van, null);
        await repository.AddAsync(existing, default);
        var sut = new RegisterVehicleCommandHandler(repository);

        var result = await sut.Handle(
            new RegisterVehicleCommand("ABC-123", 900, "truck", null),
            default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("PLATE_NUMBER_DUPLICATE");
        repository.Vehicles.Should().ContainSingle();
        repository.SaveChangesCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_InvalidVehicleType_ReturnsValidationFailureAsync()
    {
        var repository = new InMemoryVehicleRepository();
        var sut = new RegisterVehicleCommandHandler(repository);

        var result = await sut.Handle(
            new RegisterVehicleCommand("ABC-123", 1200, "boat", null),
            default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("VALIDATION_ERROR");
        repository.Vehicles.Should().BeEmpty();
    }
}
