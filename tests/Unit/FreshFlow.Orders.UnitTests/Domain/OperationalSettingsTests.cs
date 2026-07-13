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
    }

    [Fact]
    public void Update_ChangesFieldsAndTouchesUpdatedAt()
    {
        var settings = OperationalSettings.CreateDefault();
        var before = settings.UpdatedAt;

        settings.Update(new TimeOnly(21, 0), false, "direct");

        settings.DailyCutoffTime.Should().Be(new TimeOnly(21, 0));
        settings.BatchingEnabled.Should().BeFalse();
        settings.DefaultRouteType.Should().Be("direct");
        settings.UpdatedAt.Should().BeOnOrAfter(before);
    }
}
