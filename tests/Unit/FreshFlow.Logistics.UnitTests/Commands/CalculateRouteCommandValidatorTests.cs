using FluentAssertions;
using FreshFlow.Logistics.Application.Commands.CalculateRoute;

namespace FreshFlow.Logistics.UnitTests.Commands;

[Trait("Category", "Unit")]
public sealed class CalculateRouteCommandValidatorTests
{
    private readonly CalculateRouteCommandValidator _sut = new();

    [Fact]
    public void Validate_ValidHubCommand_Passes()
    {
        var result = _sut.Validate(Command());

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_HubIdMissing_Fails()
    {
        var result = _sut.Validate(Command(hubId: Guid.Empty));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_MoreThan20Stops_Passes()
    {
        var result = _sut.Validate(Command(
            destinationRestaurantIds: Enumerable.Range(0, 20).Select(_ => Guid.NewGuid()).ToList()));

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("DISTANCE")]
    [InlineData("time")]
    [InlineData("Cost")]
    [InlineData(null)]
    [InlineData("")]
    public void Validate_SupportedOptimizationCriteria_Passes(string? criteria)
    {
        var result = _sut.Validate(Command(optimizationCriteria: criteria));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_UnsupportedOptimizationCriteria_Fails()
    {
        var result = _sut.Validate(Command(optimizationCriteria: "FASTEST"));

        result.IsValid.Should().BeFalse();
    }

    private static CalculateRouteCommand Command(
        Guid? hubId = null,
        IReadOnlyList<Guid>? destinationRestaurantIds = null,
        string? optimizationCriteria = "COST") =>
        new(
            hubId ?? Guid.NewGuid(),
            destinationRestaurantIds ?? [Guid.NewGuid()],
            optimizationCriteria,
            new DateOnly(2026, 7, 9));
}
