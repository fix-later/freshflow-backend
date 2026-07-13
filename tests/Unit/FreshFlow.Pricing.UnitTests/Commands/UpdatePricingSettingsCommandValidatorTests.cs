using FluentAssertions;
using FreshFlow.Pricing.Application.Commands.UpdatePricingSettings;

namespace FreshFlow.Pricing.UnitTests.Commands;

[Trait("Category", "Unit")]
public sealed class UpdatePricingSettingsCommandValidatorTests
{
    private readonly UpdatePricingSettingsCommandValidator _sut = new();

    [Theory]
    [InlineData(0.01)]
    [InlineData(10.00)]
    [InlineData(100)]
    public async Task Validate_InRangeThreshold_Passes(decimal threshold)
    {
        var result = await _sut.ValidateAsync(new UpdatePricingSettingsCommand(threshold));

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(100.01)]
    public async Task Validate_OutOfRangeThreshold_Fails(decimal threshold)
    {
        var result = await _sut.ValidateAsync(new UpdatePricingSettingsCommand(threshold));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UpdatePricingSettingsCommand.PriceAlertThresholdPercent));
    }
}
