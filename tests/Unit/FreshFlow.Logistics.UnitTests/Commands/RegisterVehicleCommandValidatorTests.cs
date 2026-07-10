using FluentAssertions;
using FreshFlow.Logistics.Application.Commands.RegisterVehicle;

namespace FreshFlow.Logistics.UnitTests.Commands;

[Trait("Category", "Unit")]
public sealed class RegisterVehicleCommandValidatorTests
{
    private readonly RegisterVehicleCommandValidator _sut = new();

    [Fact]
    public void Validate_ValidCommand_Passes()
    {
        var result = _sut.Validate(new RegisterVehicleCommand("ABC-123", 1200, "van", null));

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Validate_BlankPlateNumber_Fails(string plateNumber)
    {
        var result = _sut.Validate(new RegisterVehicleCommand(plateNumber, 1200, "van", null));

        result.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_InvalidCapacity_Fails(decimal capacityKg)
    {
        var result = _sut.Validate(new RegisterVehicleCommand("ABC-123", capacityKg, "van", null));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_InvalidVehicleType_Fails()
    {
        var result = _sut.Validate(new RegisterVehicleCommand("ABC-123", 1200, "boat", null));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "VehicleType");
    }
}
