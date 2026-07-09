using FluentAssertions;
using FreshFlow.Logistics.Application.Commands.OptimizeRoute;

namespace FreshFlow.Logistics.UnitTests.Commands;

[Trait("Category", "Unit")]
public sealed class OptimizeRouteCommandValidatorTests
{
    private readonly OptimizeRouteCommandValidator _sut = new();

    [Theory]
    [InlineData("DISTANCE")]
    [InlineData("time")]
    [InlineData("Cost")]
    public void Validate_SupportedCriteria_Passes(string criteria)
    {
        var result = _sut.Validate(new OptimizeRouteCommand(Guid.NewGuid(), criteria));

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("FASTEST")]
    [InlineData("1")]
    public void Validate_InvalidCriteria_Fails(string criteria)
    {
        var result = _sut.Validate(new OptimizeRouteCommand(Guid.NewGuid(), criteria));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_EmptyRouteId_Fails()
    {
        var result = _sut.Validate(new OptimizeRouteCommand(Guid.Empty, "DISTANCE"));

        result.IsValid.Should().BeFalse();
    }
}
