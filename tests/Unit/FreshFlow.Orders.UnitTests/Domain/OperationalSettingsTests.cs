using FluentAssertions;
using FreshFlow.Orders.Domain.Entities;

namespace FreshFlow.Orders.UnitTests.Domain;

[Trait("Category", "Unit")]
public sealed class OperationalSettingsTests
{
    [Fact]
    public void CreateDefault_ReturnsTwentyTwoHundredHubRelayBatchingOn()
    {
        var settings = OperationalSettings.CreateDefault();

        settings.DailyCutoffTime.Should().Be(new TimeOnly(22, 0));
        settings.BatchingEnabled.Should().BeTrue();
        settings.DefaultRouteType.Should().Be("hub_relay");
        settings.DeliveryWindowDays.Should().Be(7);
    }

    [Fact]
    public void Update_ChangesFieldsAndTouchesUpdatedAt()
    {
        var settings = OperationalSettings.CreateDefault();
        var before = settings.UpdatedAt;

        settings.Update(new TimeOnly(21, 0), false, "direct", 14);

        settings.DailyCutoffTime.Should().Be(new TimeOnly(21, 0));
        settings.BatchingEnabled.Should().BeFalse();
        settings.DefaultRouteType.Should().Be("direct");
        settings.DeliveryWindowDays.Should().Be(14);
        settings.UpdatedAt.Should().BeOnOrAfter(before);
    }
}
