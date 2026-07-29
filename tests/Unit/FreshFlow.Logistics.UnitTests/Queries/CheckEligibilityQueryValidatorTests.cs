using FluentAssertions;
using FreshFlow.Logistics.Application.Queries.CheckEligibility;

namespace FreshFlow.Logistics.UnitTests.Queries;

[Trait("Category", "Unit")]
public sealed class CheckEligibilityQueryValidatorTests
{
    private readonly CheckEligibilityQueryValidator _sut = new();

    [Fact]
    public void Validate_ValidQuery_Passes()
    {
        var result = _sut.Validate(
            new CheckEligibilityQuery(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptyRouteId_Fails()
    {
        var result = _sut.Validate(
            new CheckEligibilityQuery(Guid.Empty, Guid.NewGuid(), Guid.NewGuid()));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_EmptyVehicleId_Fails()
    {
        var result = _sut.Validate(
            new CheckEligibilityQuery(Guid.NewGuid(), Guid.Empty, Guid.NewGuid()));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_MissingDriver_Fails()
    {
        var result = _sut.Validate(new CheckEligibilityQuery(Guid.NewGuid(), Guid.NewGuid(), null));

        result.IsValid.Should().BeFalse();
    }
}
