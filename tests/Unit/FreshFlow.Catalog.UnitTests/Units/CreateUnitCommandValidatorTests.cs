using FluentAssertions;
using FreshFlow.Catalog.Application.Commands.Units.Create;

namespace FreshFlow.Catalog.UnitTests.Units;

[Trait("Category", "Unit")]
public sealed class CreateUnitCommandValidatorTests
{
    private readonly CreateUnitCommandValidator _sut = new();

    [Theory]
    [InlineData("kg", null)]
    [InlineData("thùng", "thùng")]
    [InlineData("bó", "bó")]
    public async Task Validate_ValidCommand_Passes(string name, string? abbreviation)
    {
        var result = await _sut.ValidateAsync(new CreateUnitCommand(name, abbreviation));
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Validate_EmptyName_Fails(string name)
    {
        var result = await _sut.ValidateAsync(new CreateUnitCommand(name, null));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Name");
    }

    [Fact]
    public async Task Validate_NameExceedsMaxLength_Fails()
    {
        var longName = new string('x', 101);
        var result = await _sut.ValidateAsync(new CreateUnitCommand(longName, null));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Name");
    }

    [Fact]
    public async Task Validate_AbbreviationExceedsMaxLength_Fails()
    {
        var longAbbr = new string('x', 21);
        var result = await _sut.ValidateAsync(new CreateUnitCommand("kg", longAbbr));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Abbreviation");
    }
}
