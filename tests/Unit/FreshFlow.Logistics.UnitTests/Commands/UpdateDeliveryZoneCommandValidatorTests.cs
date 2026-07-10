using FluentAssertions;
using FreshFlow.Logistics.Application.Commands.DeliveryZones.Update;

namespace FreshFlow.Logistics.UnitTests.Commands;

[Trait("Category", "Unit")]
public sealed class UpdateDeliveryZoneCommandValidatorTests
{
    private readonly UpdateDeliveryZoneCommandValidator _sut = new();

    [Fact]
    public void Validate_ValidCommand_Passes()
    {
        var result = _sut.Validate(new UpdateDeliveryZoneCommand(Guid.NewGuid(), "District 1", null));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptyId_Fails()
    {
        var result = _sut.Validate(new UpdateDeliveryZoneCommand(Guid.Empty, "District 1", null));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_BlankName_Fails()
    {
        var result = _sut.Validate(new UpdateDeliveryZoneCommand(Guid.NewGuid(), " ", null));

        result.IsValid.Should().BeFalse();
    }
}
