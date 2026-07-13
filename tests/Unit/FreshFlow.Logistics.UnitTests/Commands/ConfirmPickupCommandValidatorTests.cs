using FluentAssertions;
using FreshFlow.Logistics.Application.Commands.ConfirmPickup;

namespace FreshFlow.Logistics.UnitTests.Commands;

[Trait("Category", "Unit")]
public sealed class ConfirmPickupCommandValidatorTests
{
    private readonly ConfirmPickupCommandValidator _sut = new();

    [Fact]
    public void Validate_ValidCommand_Passes()
    {
        var result = _sut.Validate(new ConfirmPickupCommand(Guid.NewGuid(), Guid.NewGuid(), [Guid.NewGuid()]));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptyRouteId_Fails()
    {
        var result = _sut.Validate(new ConfirmPickupCommand(Guid.Empty, Guid.NewGuid(), [Guid.NewGuid()]));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_EmptyDriverUserId_Fails()
    {
        var result = _sut.Validate(new ConfirmPickupCommand(Guid.NewGuid(), Guid.Empty, [Guid.NewGuid()]));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_EmptyOrderIds_Fails()
    {
        var result = _sut.Validate(new ConfirmPickupCommand(Guid.NewGuid(), Guid.NewGuid(), []));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_EmptyOrderId_Fails()
    {
        var result = _sut.Validate(new ConfirmPickupCommand(Guid.NewGuid(), Guid.NewGuid(), [Guid.Empty]));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_DuplicateOrderIds_Fails()
    {
        var orderId = Guid.NewGuid();

        var result = _sut.Validate(new ConfirmPickupCommand(Guid.NewGuid(), Guid.NewGuid(), [orderId, orderId]));

        result.IsValid.Should().BeFalse();
    }
}
