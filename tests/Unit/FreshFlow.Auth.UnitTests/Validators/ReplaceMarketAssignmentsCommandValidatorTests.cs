using FluentAssertions;
using FreshFlow.Auth.Application.Commands.Admin.ReplaceMarketAssignments;

namespace FreshFlow.Auth.UnitTests.Validators;

[Trait("Category", "Unit")]
public sealed class ReplaceMarketAssignmentsCommandValidatorTests
{
    private readonly ReplaceMarketAssignmentsCommandValidator _sut = new();

    [Fact]
    public async Task Validate_ValidCommandWithMarkets_Passes()
    {
        var cmd = new ReplaceMarketAssignmentsCommand(Guid.NewGuid(), [Guid.NewGuid()], null);
        var result = await _sut.ValidateAsync(cmd);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_EmptyMarketIdsList_Passes()
    {
        // Empty list = clear all assignments — valid
        var cmd = new ReplaceMarketAssignmentsCommand(Guid.NewGuid(), [], null);
        var result = await _sut.ValidateAsync(cmd);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_EmptyUserId_Fails()
    {
        var cmd = new ReplaceMarketAssignmentsCommand(Guid.Empty, [], null);
        var result = await _sut.ValidateAsync(cmd);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "UserId");
    }

    [Fact]
    public async Task Validate_EmptyGuidInMarketIds_Fails()
    {
        var cmd = new ReplaceMarketAssignmentsCommand(
            Guid.NewGuid(),
            [Guid.NewGuid(), Guid.Empty],
            null);
        var result = await _sut.ValidateAsync(cmd);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName.Contains("MarketIds"));
    }

    [Fact]
    public async Task Validate_DuplicateMarketIds_Fails()
    {
        var dup = Guid.NewGuid();
        var cmd = new ReplaceMarketAssignmentsCommand(Guid.NewGuid(), [dup, dup], null);
        var result = await _sut.ValidateAsync(cmd);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "MarketIds");
    }
}
