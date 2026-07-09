using FluentAssertions;
using FreshFlow.Logistics.Domain.Entities;

namespace FreshFlow.Logistics.UnitTests.Domain;

[Trait("Category", "Unit")]
public sealed class DeliveryZoneTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_BlankCode_ThrowsArgumentException(string code)
    {
        var act = () => new DeliveryZone(code, "District 1", null);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_BlankName_ThrowsArgumentException(string name)
    {
        var act = () => new DeliveryZone("DISTRICT_1", name, null);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_NormalizesCodeAndTrimsFields()
    {
        var zone = new DeliveryZone(" district_1 ", " District 1 ", " Central district ");

        zone.Code.Should().Be("DISTRICT_1");
        zone.Name.Should().Be("District 1");
        zone.Description.Should().Be("Central district");
        zone.IsActive.Should().BeTrue();
        zone.DeletedAt.Should().BeNull();
    }

    [Fact]
    public void Update_ChangesNameAndDescriptionButNotCode()
    {
        var zone = new DeliveryZone("DISTRICT_1", "District 1", "Old");

        zone.Update(" District One ", " New description ");

        zone.Code.Should().Be("DISTRICT_1");
        zone.Name.Should().Be("District One");
        zone.Description.Should().Be("New description");
    }

    [Fact]
    public void Update_DoesNotAcceptCodeParameter()
    {
        var parameters = typeof(DeliveryZone)
            .GetMethod(nameof(DeliveryZone.Update))!
            .GetParameters()
            .Select(p => p.Name)
            .ToArray();

        parameters.Should().Equal("name", "description");
    }

    [Fact]
    public void Deactivate_IsIdempotentAndKeepsOriginalDeletedAt()
    {
        var zone = new DeliveryZone("DISTRICT_1", "District 1", null);

        zone.Deactivate();
        var deletedAt = zone.DeletedAt;
        zone.Deactivate();

        zone.IsActive.Should().BeFalse();
        zone.DeletedAt.Should().Be(deletedAt);
    }

    [Fact]
    public void Activate_SetsActiveAndUpdatesTimestamp()
    {
        var zone = new DeliveryZone("DISTRICT_1", "District 1", null);
        var originalUpdatedAt = zone.UpdatedAt;

        zone.Activate();

        zone.IsActive.Should().BeTrue();
        zone.UpdatedAt.Should().BeOnOrAfter(originalUpdatedAt);
    }
}
