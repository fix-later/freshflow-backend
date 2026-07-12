using FluentAssertions;
using FreshFlow.Logistics.Application.Commands.StartRoute;

namespace FreshFlow.Logistics.UnitTests.Commands;

[Trait("Category", "Unit")]
public sealed class StartRouteCommandValidatorTests
{
    [Fact]
    public void Validate_EmptyRouteId_Fails()
    {
        var sut = new StartRouteCommandValidator();

        var result = sut.Validate(new StartRouteCommand(Guid.Empty, Guid.NewGuid()));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_EmptyDriverUserId_Fails()
    {
        var sut = new StartRouteCommandValidator();

        var result = sut.Validate(new StartRouteCommand(Guid.NewGuid(), Guid.Empty));

        result.IsValid.Should().BeFalse();
    }
}
