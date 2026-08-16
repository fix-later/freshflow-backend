using FluentAssertions;
using FreshFlow.Logistics.Application.Queries.GetVehicle;
using FreshFlow.Logistics.Domain.Entities;
using FreshFlow.Logistics.Domain.Enums;
using FreshFlow.Logistics.UnitTests.TestDoubles;

namespace FreshFlow.Logistics.UnitTests.Queries;

[Trait("Category", "Unit")]
public sealed class GetVehicleQueryHandlerTests
{
    [Fact]
    public async Task Handle_ExistingVehicle_ReturnsDtoAsync()
    {
        var repository = new InMemoryVehicleRepository();
        var vehicle = new Vehicle("ABC-123", 1200, VehicleType.van, null);
        await repository.AddAsync(vehicle, default);
        var sut = new GetVehicleQueryHandler(repository);

        var result = await sut.Handle(new GetVehicleQuery(vehicle.Id), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(vehicle.Id);
        result.Value.PlateNumber.Should().Be("ABC-123");
        result.Value.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_MissingVehicle_ReturnsNotFoundAsync()
    {
        var repository = new InMemoryVehicleRepository();
        var sut = new GetVehicleQueryHandler(repository);

        var result = await sut.Handle(new GetVehicleQuery(Guid.NewGuid()), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("VEHICLE_NOT_FOUND");
    }
}
