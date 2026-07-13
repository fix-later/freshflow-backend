using FluentAssertions;
using FreshFlow.Logistics.Application.Commands.UpdateDeliveryStatus;

namespace FreshFlow.Logistics.UnitTests.Commands;

[Trait("Category", "Unit")]
public sealed class UpdateDeliveryStatusCommandValidatorTests
{
    private readonly UpdateDeliveryStatusCommandValidator _sut = new();

    [Theory]
    [InlineData("ARRIVED")]
    [InlineData("DELIVERED")]
    [InlineData("FAILED")]
    public void Validate_AllowedStatus_Passes(string status)
    {
        var reason = status == "FAILED" ? "failed" : null;

        var result = _sut.Validate(new UpdateDeliveryStatusCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            status,
            reason));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_UnknownStatus_Fails()
    {
        var result = _sut.Validate(new UpdateDeliveryStatusCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "PENDING",
            null));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_FailedWithoutReason_Fails()
    {
        var result = _sut.Validate(new UpdateDeliveryStatusCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "FAILED",
            " "));

        result.IsValid.Should().BeFalse();
    }
}
