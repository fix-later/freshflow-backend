using FluentAssertions;
using FreshFlow.Catalog.Application.Commands.Markets.Update;

namespace FreshFlow.Catalog.UnitTests.Markets;

[Trait("Category", "Unit")]
public sealed class UpdateMarketCommandValidatorTests
{
    private readonly UpdateMarketCommandValidator _sut = new();

    [Fact]
    public async Task Validate_ValidCommand_Passes()
    {
        var result = await _sut.ValidateAsync(
            new UpdateMarketCommand(Guid.NewGuid(), "Bình Điền Market", "Bình Điền", null, null, null));
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_EmptyId_Fails()
    {
        var result = await _sut.ValidateAsync(
            new UpdateMarketCommand(Guid.Empty, "Name", null, null, null, null));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Id");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Validate_EmptyName_Fails(string name)
    {
        var result = await _sut.ValidateAsync(
            new UpdateMarketCommand(Guid.NewGuid(), name, null, null, null, null));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Name");
    }

    [Theory]
    [InlineData(-91.0)]
    [InlineData(91.0)]
    public async Task Validate_InvalidLatitude_Fails(double lat)
    {
        var result = await _sut.ValidateAsync(
            new UpdateMarketCommand(Guid.NewGuid(), "Name", null, null, (decimal)lat, null));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName.Contains("Latitude"));
    }
    [Fact]
    public async Task Validate_InvalidImageUrl_Fails()
    {
        var result = await _sut.ValidateAsync(
            new UpdateMarketCommand(Guid.NewGuid(), "Name", null, null, null, null, "not-a-url"));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "ImageUrl");
    }
}
