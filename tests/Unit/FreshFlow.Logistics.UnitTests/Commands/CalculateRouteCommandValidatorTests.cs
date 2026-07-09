using FluentAssertions;
using FreshFlow.Logistics.Application.Commands.CalculateRoute;

namespace FreshFlow.Logistics.UnitTests.Commands;

[Trait("Category", "Unit")]
public sealed class CalculateRouteCommandValidatorTests
{
    private readonly CalculateRouteCommandValidator _sut = new();

    [Fact]
    public void Validate_ValidDirectCommand_Passes()
    {
        var result = _sut.Validate(Command());

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_HubIdsPresent_FailsWithHubRelayNotSupported()
    {
        var result = _sut.Validate(Command(hubIds: [Guid.NewGuid()]));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.ErrorCode == "HUB_RELAY_NOT_SUPPORTED");
    }

    [Fact]
    public void Validate_CompareWithHubTrue_FailsWithHubRelayNotSupported()
    {
        var result = _sut.Validate(Command(compareWithHub: true));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.ErrorCode == "HUB_RELAY_NOT_SUPPORTED");
    }

    [Fact]
    public void Validate_MoreThan20Stops_FailsWithStopLimitExceeded()
    {
        var result = _sut.Validate(Command(
            sourceMarketIds: Enumerable.Range(0, 10).Select(_ => Guid.NewGuid()).ToList(),
            destinationRestaurantIds: Enumerable.Range(0, 11).Select(_ => Guid.NewGuid()).ToList()));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.ErrorCode == "STOP_LIMIT_EXCEEDED");
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
        IReadOnlyList<Guid>? sourceMarketIds = null,
        IReadOnlyList<Guid>? hubIds = null,
        IReadOnlyList<Guid>? destinationRestaurantIds = null,
        string? optimizationCriteria = "COST",
        bool compareWithHub = false) =>
        new(
            sourceMarketIds ?? [Guid.NewGuid()],
            hubIds ?? [],
            destinationRestaurantIds ?? [Guid.NewGuid()],
            optimizationCriteria,
            new DateOnly(2026, 7, 9),
            compareWithHub);
}
