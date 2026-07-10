using FluentAssertions;
using FreshFlow.Logistics.Application.Commands.DeliveryZones.Deactivate;

namespace FreshFlow.Logistics.UnitTests.Commands;

[Trait("Category", "Unit")]
public sealed class DeactivateDeliveryZoneCommandValidatorTests
{
    [Fact]
    public void Validate_EmptyId_Fails()
    {
        var sut = new DeactivateDeliveryZoneCommandValidator();

        var result = sut.Validate(new DeactivateDeliveryZoneCommand(Guid.Empty));

        result.IsValid.Should().BeFalse();
    }
}
