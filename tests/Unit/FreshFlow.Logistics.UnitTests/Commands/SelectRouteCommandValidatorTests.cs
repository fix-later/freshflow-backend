using FluentAssertions;
using FreshFlow.Logistics.Application.Commands.SelectRoute;

namespace FreshFlow.Logistics.UnitTests.Commands;

[Trait("Category", "Unit")]
public sealed class SelectRouteCommandValidatorTests
{
    private readonly SelectRouteCommandValidator _sut = new();

    [Fact]
    public void Validate_EmptyRouteId_Fails()
    {
        var result = _sut.Validate(new SelectRouteCommand(Guid.Empty));

        result.IsValid.Should().BeFalse();
    }
}
