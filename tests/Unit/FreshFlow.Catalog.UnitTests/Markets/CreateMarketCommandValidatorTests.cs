using FluentAssertions;
using FreshFlow.Catalog.Application.Commands.Markets.Create;

namespace FreshFlow.Catalog.UnitTests.Markets;

[Trait("Category", "Unit")]
public sealed class CreateMarketCommandValidatorTests
{
    private readonly CreateMarketCommandValidator _sut = new();

    [Fact]
    public async Task Validate_ValidCommand_Passes()
    {
        var result = await _sut.ValidateAsync(
            new CreateMarketCommand("Hóc Môn Market", "Hóc Môn", "123 St", 10.8m, 106.6m));
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Validate_EmptyName_Fails(string name)
    {
        var result = await _sut.ValidateAsync(new CreateMarketCommand(name, null, null, null, null));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Name");
    }

    [Fact]
    public async Task Validate_NameExceedsMaxLength_Fails()
    {
        var longName = new string('x', 201);
        var result = await _sut.ValidateAsync(new CreateMarketCommand(longName, null, null, null, null));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Name");
    }

    [Theory]
    [InlineData(-91.0)]
    [InlineData(91.0)]
    public async Task Validate_InvalidLatitude_Fails(double lat)
    {
        var result = await _sut.ValidateAsync(
            new CreateMarketCommand("Name", null, null, (decimal)lat, null));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName.Contains("Latitude"));
    }

    [Theory]
    [InlineData(-181.0)]
    [InlineData(181.0)]
    public async Task Validate_InvalidLongitude_Fails(double lng)
    {
        var result = await _sut.ValidateAsync(
            new CreateMarketCommand("Name", null, null, null, (decimal)lng));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName.Contains("Longitude"));
    }

    [Fact]
    public async Task Validate_NullOptionalFields_Passes()
    {
        var result = await _sut.ValidateAsync(new CreateMarketCommand("Name", null, null, null, null));
        result.IsValid.Should().BeTrue();
    }
}
