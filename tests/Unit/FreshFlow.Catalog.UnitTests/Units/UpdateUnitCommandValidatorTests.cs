using FluentAssertions;
using FreshFlow.Catalog.Application.Commands.Units.Update;

namespace FreshFlow.Catalog.UnitTests.Units;

[Trait("Category", "Unit")]
public sealed class UpdateUnitCommandValidatorTests
{
    private readonly UpdateUnitCommandValidator _sut = new();

    [Theory]
    [InlineData("kilogram", "kg")]
    [InlineData("thùng", null)]
    public async Task Validate_ValidCommand_Passes(string name, string? abbreviation)
    {
        var result = await _sut.ValidateAsync(new UpdateUnitCommand(Guid.NewGuid(), name, abbreviation));
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Validate_EmptyName_Fails(string name)
    {
        var result = await _sut.ValidateAsync(new UpdateUnitCommand(Guid.NewGuid(), name, null));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Name");
    }

    [Fact]
    public async Task Validate_EmptyId_Fails()
    {
        var result = await _sut.ValidateAsync(new UpdateUnitCommand(Guid.Empty, "kg", null));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Id");
    }

    [Fact]
    public async Task Validate_AbbreviationExceedsMaxLength_Fails()
    {
        var longAbbr = new string('x', 21);
        var result = await _sut.ValidateAsync(new UpdateUnitCommand(Guid.NewGuid(), "kg", longAbbr));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Abbreviation");
    }
}
