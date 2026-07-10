using FluentAssertions;
using HubEntity = FreshFlow.Hub.Domain.Entities.Hub;

namespace FreshFlow.Hub.UnitTests.Domain;

[Trait("Category", "Unit")]
public sealed class HubTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_InvalidCapacity_ThrowsArgumentException(decimal capacityKg)
    {
        var act = () => HubEntity.Create("Main Hub", null, null, null, capacityKg, null);

        act.Should().Throw<ArgumentException>()
            .WithParameterName("capacityKg");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Create_BlankName_ThrowsArgumentException(string name)
    {
        var act = () => HubEntity.Create(name, null, null, null, 1000, null);

        act.Should().Throw<ArgumentException>()
            .WithParameterName("name");
    }

    [Theory]
    [InlineData(-90.1)]
    [InlineData(90.1)]
    public void Create_InvalidLatitude_ThrowsArgumentOutOfRangeException(decimal latitude)
    {
        var act = () => HubEntity.Create("Main Hub", null, latitude, null, 1000, null);

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("latitude");
    }

    [Theory]
    [InlineData(-180.1)]
    [InlineData(180.1)]
    public void Create_InvalidLongitude_ThrowsArgumentOutOfRangeException(decimal longitude)
    {
        var act = () => HubEntity.Create("Main Hub", null, null, longitude, 1000, null);

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("longitude");
    }

    [Fact]
    public void Create_ValidInput_SetsDefaults()
    {
        var managedBy = Guid.NewGuid();

        var hub = HubEntity.Create(" Main Hub ", " 123 Road ", 10.123456m, 106.123456m, 1000, managedBy);

        hub.Id.Should().NotBeEmpty();
        hub.Name.Should().Be("Main Hub");
        hub.Address.Should().Be("123 Road");
        hub.Latitude.Should().Be(10.123456m);
        hub.Longitude.Should().Be(106.123456m);
        hub.CapacityKg.Should().Be(1000);
        hub.OccupiedCapacityKg.Should().Be(0);
        hub.AvailableCapacityKg.Should().Be(1000);
        hub.IsActive.Should().BeTrue();
        hub.ManagedBy.Should().Be(managedBy);
        hub.DeletedAt.Should().BeNull();
        hub.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        hub.UpdatedAt.Should().Be(hub.CreatedAt);
    }

    [Fact]
    public void Update_ValidInput_UpdatesEditableFieldsOnly()
    {
        var managedBy = Guid.NewGuid();
        var hub = HubEntity.Create("Main Hub", "123 Road", 10m, 106m, 1000, managedBy);
        var updatedManagedBy = Guid.NewGuid();
        var originalUpdatedAt = hub.UpdatedAt;

        hub.Update(" Updated Hub ", " 456 Road ", 11m, 107m, 1500, updatedManagedBy);

        hub.Name.Should().Be("Updated Hub");
        hub.Address.Should().Be("456 Road");
        hub.Latitude.Should().Be(11m);
        hub.Longitude.Should().Be(107m);
        hub.CapacityKg.Should().Be(1500);
        hub.ManagedBy.Should().Be(updatedManagedBy);
        hub.IsActive.Should().BeTrue();
        hub.UpdatedAt.Should().BeOnOrAfter(originalUpdatedAt);
    }

    [Fact]
    public void ApplyInbound_IncreasesOccupiedCapacity()
    {
        var hub = HubEntity.Create("Main Hub", null, null, null, 1000, null);
        var originalUpdatedAt = hub.UpdatedAt;

        hub.ApplyInbound(125.5m);

        hub.OccupiedCapacityKg.Should().Be(125.5m);
        hub.AvailableCapacityKg.Should().Be(874.5m);
        hub.UpdatedAt.Should().BeOnOrAfter(originalUpdatedAt);
    }

    [Fact]
    public void Deactivate_FirstCall_SetsInactiveAndIsIdempotent()
    {
        var hub = HubEntity.Create("Main Hub", null, null, null, 1000, null);

        hub.Deactivate();
        var firstUpdatedAt = hub.UpdatedAt;
        hub.Deactivate();

        hub.IsActive.Should().BeFalse();
        hub.UpdatedAt.Should().Be(firstUpdatedAt);
        hub.DeletedAt.Should().BeNull();
    }
}
