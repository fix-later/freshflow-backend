using FluentAssertions;
using FreshFlow.Logistics.Domain.Entities;
using FreshFlow.Logistics.Domain.Enums;

namespace FreshFlow.Logistics.UnitTests.Domain;

[Trait("Category", "Unit")]
public sealed class VehicleTests
{
    [Fact]
    public void AssignHub_EmptyHubId_ThrowsArgumentException()
    {
        var vehicle = new Vehicle("ABC-123", 1200, VehicleType.van, null);

        var act = () => vehicle.AssignHub(Guid.Empty);

        act.Should().Throw<ArgumentException>().WithParameterName("hubId");
    }

    [Fact]
    public void AssignHub_ValidHubId_AssignsHubAndUpdatesTimestamp()
    {
        var vehicle = new Vehicle("ABC-123", 1200, VehicleType.van, null);
        var hubId = Guid.NewGuid();
        var previousUpdatedAt = vehicle.UpdatedAt;

        vehicle.AssignHub(hubId);

        vehicle.HubId.Should().Be(hubId);
        vehicle.UpdatedAt.Should().BeOnOrAfter(previousUpdatedAt);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_InvalidCapacity_ThrowsArgumentException(decimal capacityKg)
    {
        var act = () => new Vehicle("ABC-123", capacityKg, VehicleType.van, null);

        act.Should().Throw<ArgumentException>()
            .WithParameterName("capacityKg");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Constructor_BlankPlateNumber_ThrowsArgumentException(string plateNumber)
    {
        var act = () => new Vehicle(plateNumber, 1000, VehicleType.van, null);

        act.Should().Throw<ArgumentException>()
            .WithParameterName("plateNumber");
    }

    [Fact]
    public void Constructor_ValidInput_SetsDefaults()
    {
        var registeredBy = Guid.NewGuid();

        var vehicle = new Vehicle(" ABC-123 ", 1200, VehicleType.truck, registeredBy);

        vehicle.Id.Should().NotBeEmpty();
        vehicle.PlateNumber.Should().Be("ABC-123");
        vehicle.CapacityKg.Should().Be(1200);
        vehicle.VehicleType.Should().Be(VehicleType.truck);
        vehicle.RegisteredBy.Should().Be(registeredBy);
        vehicle.IsAvailable.Should().BeTrue();
        vehicle.DeletedAt.Should().BeNull();
        vehicle.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        vehicle.UpdatedAt.Should().Be(vehicle.CreatedAt);
    }

    [Fact]
    public void Update_ValidInput_UpdatesFieldsAndTimestamp()
    {
        var hubId = Guid.NewGuid();
        var vehicle = new Vehicle("ABC-123", 1200, VehicleType.van, null, hubId);
        var originalUpdatedAt = vehicle.UpdatedAt;

        vehicle.Update(" XYZ-789 ", 2400, VehicleType.truck);

        vehicle.PlateNumber.Should().Be("XYZ-789");
        vehicle.CapacityKg.Should().Be(2400);
        vehicle.VehicleType.Should().Be(VehicleType.truck);
        vehicle.HubId.Should().Be(hubId);
        vehicle.UpdatedAt.Should().BeOnOrAfter(originalUpdatedAt);
    }

    [Fact]
    public void Deactivate_FirstCall_SetsDeletedAtAndIsIdempotent()
    {
        var vehicle = new Vehicle("ABC-123", 1200, VehicleType.van, null);

        vehicle.Deactivate();
        var firstDeletedAt = vehicle.DeletedAt;
        var firstUpdatedAt = vehicle.UpdatedAt;
        vehicle.Deactivate();

        vehicle.DeletedAt.Should().Be(firstDeletedAt);
        vehicle.UpdatedAt.Should().Be(firstUpdatedAt);
    }

    [Fact]
    public void MarkAvailableAndUnavailable_ToggleAvailability()
    {
        var vehicle = new Vehicle("ABC-123", 1200, VehicleType.van, null);

        vehicle.MarkUnavailable();
        var unavailableUpdatedAt = vehicle.UpdatedAt;
        vehicle.MarkAvailable();

        vehicle.IsAvailable.Should().BeTrue();
        vehicle.UpdatedAt.Should().BeOnOrAfter(unavailableUpdatedAt);
    }
}
