using FluentAssertions;
using FreshFlow.Orders.Application.Commands.UpdateOperationalSettings;

namespace FreshFlow.Orders.UnitTests.Commands;

[Trait("Category", "Unit")]
public sealed class UpdateOperationalSettingsCommandValidatorTests
{
    private readonly UpdateOperationalSettingsCommandValidator _sut = new();

    [Theory]
    [InlineData("hub_relay")]
    [InlineData("direct")]
    public async Task Validate_ValidRouteType_Passes(string routeType)
    {
        var result = await _sut.ValidateAsync(
            new UpdateOperationalSettingsCommand(new TimeOnly(22, 0), true, routeType, 7));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_InvalidRouteType_Fails()
    {
        var result = await _sut.ValidateAsync(
            new UpdateOperationalSettingsCommand(new TimeOnly(22, 0), true, "teleport", 7));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UpdateOperationalSettingsCommand.DefaultRouteType));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(30)]
    public async Task Validate_DeliveryWindowDaysInRange_Passes(int days)
    {
        var result = await _sut.ValidateAsync(
            new UpdateOperationalSettingsCommand(new TimeOnly(22, 0), true, "hub_relay", days));

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(31)]
    public async Task Validate_DeliveryWindowDaysOutOfRange_Fails(int days)
    {
        var result = await _sut.ValidateAsync(
            new UpdateOperationalSettingsCommand(new TimeOnly(22, 0), true, "hub_relay", days));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UpdateOperationalSettingsCommand.DeliveryWindowDays));
    }
}
