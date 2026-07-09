using FluentAssertions;
using FreshFlow.Logistics.Application.Commands.DeliveryZones.Create;

namespace FreshFlow.Logistics.UnitTests.Commands;

[Trait("Category", "Unit")]
public sealed class CreateDeliveryZoneCommandValidatorTests
{
    private readonly CreateDeliveryZoneCommandValidator _sut = new();

    [Fact]
    public void Validate_ValidCommand_Passes()
    {
        var result = _sut.Validate(new CreateDeliveryZoneCommand("DISTRICT_1", "District 1", "Central"));

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("DISTRICT-1")]
    [InlineData("DISTRICT 1")]
    public void Validate_InvalidCode_Fails(string code)
    {
        var result = _sut.Validate(new CreateDeliveryZoneCommand(code, "District 1", null));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_LongDescription_Fails()
    {
        var result = _sut.Validate(new CreateDeliveryZoneCommand("DISTRICT_1", "District 1", new string('x', 501)));

        result.IsValid.Should().BeFalse();
    }
}
