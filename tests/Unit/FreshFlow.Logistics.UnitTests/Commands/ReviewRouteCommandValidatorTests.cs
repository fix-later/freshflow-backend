using FluentAssertions;
using FreshFlow.Logistics.Application.Commands.ReviewRoute;

namespace FreshFlow.Logistics.UnitTests.Commands;

[Trait("Category", "Unit")]
public sealed class ReviewRouteCommandValidatorTests
{
    private readonly ReviewRouteCommandValidator _sut = new();

    [Fact]
    public void Validate_ValidCommand_Passes()
    {
        var result = _sut.Validate(new ReviewRouteCommand(Guid.NewGuid(), [Guid.NewGuid()]));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptyRouteId_Fails()
    {
        var result = _sut.Validate(new ReviewRouteCommand(Guid.Empty, [Guid.NewGuid()]));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_EmptyStopId_Fails()
    {
        var result = _sut.Validate(new ReviewRouteCommand(Guid.NewGuid(), [Guid.Empty]));

        result.IsValid.Should().BeFalse();
    }
}
