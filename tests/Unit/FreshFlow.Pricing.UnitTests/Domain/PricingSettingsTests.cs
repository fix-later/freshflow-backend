using FluentAssertions;
using FreshFlow.Pricing.Domain.Entities;

namespace FreshFlow.Pricing.UnitTests.Domain;

[Trait("Category", "Unit")]
public sealed class PricingSettingsTests
{
    [Fact]
    public void CreateDefault_ReturnsTenPercentThreshold()
    {
        var settings = PricingSettings.CreateDefault();

        settings.PriceAlertThresholdPercent.Should().Be(10.00m);
    }

    [Fact]
    public void Update_ChangesThresholdAndTouchesUpdatedAt()
    {
        var settings = PricingSettings.CreateDefault();
        var before = settings.UpdatedAt;

        settings.Update(15.50m);

        settings.PriceAlertThresholdPercent.Should().Be(15.50m);
        settings.UpdatedAt.Should().BeOnOrAfter(before);
    }
}
