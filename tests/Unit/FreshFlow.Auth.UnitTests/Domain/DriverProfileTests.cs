using FluentAssertions;
using FreshFlow.Auth.Domain.Entities;

namespace FreshFlow.Auth.UnitTests.Domain;

[Trait("Category", "Unit")]
public sealed class DriverProfileTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        var userId = Guid.NewGuid();

        var profile = new DriverProfile(userId);

        profile.Id.Should().NotBe(Guid.Empty);
        profile.UserId.Should().Be(userId);
        profile.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        profile.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Constructor_LicensePlateAndPhoneNumber_AreNullByDefault()
    {
        var profile = new DriverProfile(Guid.NewGuid());

        profile.LicensePlate.Should().BeNull();
        profile.PhoneNumber.Should().BeNull();
    }
}
