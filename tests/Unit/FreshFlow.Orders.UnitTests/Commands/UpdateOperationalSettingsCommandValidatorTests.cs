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

    [Fact]
    public async Task Validate_FeeWithMoreThanTwoDecimalPlaces_Fails()
    {
        var result = await _sut.ValidateAsync(new UpdateOperationalSettingsCommand(
            new TimeOnly(22, 0), true, "hub_relay", 7,
            DeliveryFeePerKm: 1.001m,
            BaseFee: 2.001m,
            MinimumFee: 3.001m,
            RoundingUnit: 4.001m));

        result.Errors.Select(error => error.PropertyName).Should().Contain([
            nameof(UpdateOperationalSettingsCommand.DeliveryFeePerKm),
            nameof(UpdateOperationalSettingsCommand.BaseFee),
            nameof(UpdateOperationalSettingsCommand.MinimumFee),
            nameof(UpdateOperationalSettingsCommand.RoundingUnit)
        ]);
    }
}
