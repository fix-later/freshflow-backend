using FluentAssertions;
using FreshFlow.Catalog.Application.Commands.Categories.Create;

namespace FreshFlow.Catalog.UnitTests.Categories;

[Trait("Category", "Unit")]
public sealed class CreateCategoryCommandValidatorTests
{
    private readonly CreateCategoryCommandValidator _sut = new();

    [Fact]
    public async Task Validate_ValidName_Passes()
    {
        var result = await _sut.ValidateAsync(new CreateCategoryCommand("Rau củ"));
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Validate_EmptyName_Fails(string name)
    {
        var result = await _sut.ValidateAsync(new CreateCategoryCommand(name));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Name");
    }

    [Fact]
    public async Task Validate_NameExceedsMaxLength_Fails()
    {
        var longName = new string('x', 201);
        var result = await _sut.ValidateAsync(new CreateCategoryCommand(longName));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Name");
    }
    [Fact]
    public async Task Validate_InvalidImageUrl_Fails()
    {
        var result = await _sut.ValidateAsync(
            new CreateCategoryCommand("Rau củ", ImageUrl: "not-a-url"));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "ImageUrl");
    }
}
