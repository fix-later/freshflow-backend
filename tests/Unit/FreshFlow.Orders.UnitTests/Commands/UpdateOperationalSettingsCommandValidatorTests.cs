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
            new UpdateOperationalSettingsCommand(new TimeOnly(22, 0), true, routeType));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_InvalidRouteType_Fails()
    {
        var result = await _sut.ValidateAsync(
            new UpdateOperationalSettingsCommand(new TimeOnly(22, 0), true, "teleport"));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UpdateOperationalSettingsCommand.DefaultRouteType));
    }
}
