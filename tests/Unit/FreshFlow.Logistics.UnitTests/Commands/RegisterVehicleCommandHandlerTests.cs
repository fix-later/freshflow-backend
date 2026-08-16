using FluentAssertions;
using FreshFlow.Logistics.Application.Abstractions;
using FreshFlow.Logistics.Application.Commands.RegisterVehicle;
using FreshFlow.Logistics.Domain.Entities;
using FreshFlow.Logistics.Domain.Enums;
using FreshFlow.Logistics.UnitTests.TestDoubles;
using NSubstitute;

namespace FreshFlow.Logistics.UnitTests.Commands;

[Trait("Category", "Unit")]
public sealed class RegisterVehicleCommandHandlerTests
{
    [Fact]
    public async Task Handle_NewVehicle_CreatesAvailableActiveVehicleAsync()
    {
        var repository = new InMemoryVehicleRepository();
        var hubReader = Substitute.For<IHubCoordinateReader>();
        var sut = new RegisterVehicleCommandHandler(repository, hubReader);
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
        await hubReader.DidNotReceiveWithAnyArgs().FindByIdAsync(default, default);
    }

    [Fact]
    public async Task Handle_DuplicatePlateNumber_ReturnsFailureAndDoesNotCreateRecordAsync()
    {
        var repository = new InMemoryVehicleRepository();
        var existing = new Vehicle("ABC-123", 1200, VehicleType.van, null);
        await repository.AddAsync(existing, default);
        var sut = new RegisterVehicleCommandHandler(repository, Substitute.For<IHubCoordinateReader>());

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
        var sut = new RegisterVehicleCommandHandler(repository, Substitute.For<IHubCoordinateReader>());

        var result = await sut.Handle(
            new RegisterVehicleCommand("ABC-123", 1200, "boat", null),
            default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("VALIDATION_ERROR");
        repository.Vehicles.Should().BeEmpty();
    }

    [Theory]
    [InlineData("999")]
    [InlineData("-1")]
    public async Task Handle_NumericStringVehicleType_ReturnsValidationFailureAsync(string vehicleType)
    {
        var repository = new InMemoryVehicleRepository();
        var sut = new RegisterVehicleCommandHandler(repository, Substitute.For<IHubCoordinateReader>());

        var result = await sut.Handle(
            new RegisterVehicleCommand("ABC-123", 1200, vehicleType, null),
            default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("VALIDATION_ERROR");
        repository.Vehicles.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WithHub_AssignsExistingHubAsync()
    {
        var repository = new InMemoryVehicleRepository();
        var hubReader = Substitute.For<IHubCoordinateReader>();
        var hubId = Guid.NewGuid();
        hubReader.FindByIdAsync(hubId, Arg.Any<CancellationToken>())
            .Returns(new HubCoordinateDto(hubId, Guid.NewGuid(), "Hub", 10m, 106m));
        var sut = new RegisterVehicleCommandHandler(repository, hubReader);

        var result = await sut.Handle(
            new RegisterVehicleCommand("ABC-123", 1200, "van", null, hubId), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.HubId.Should().Be(hubId);
        repository.Vehicles.Single().HubId.Should().Be(hubId);
    }

    [Fact]
    public async Task Handle_HubMissing_ReturnsHubNotFoundAsync()
    {
        var repository = new InMemoryVehicleRepository();
        var hubReader = Substitute.For<IHubCoordinateReader>();
        var hubId = Guid.NewGuid();
        var sut = new RegisterVehicleCommandHandler(repository, hubReader);

        var result = await sut.Handle(
            new RegisterVehicleCommand("ABC-123", 1200, "van", null, hubId), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("HUB_NOT_FOUND");
        repository.Vehicles.Should().BeEmpty();
    }
}
