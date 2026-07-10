using FluentAssertions;
using FreshFlow.Catalog.Application.Commands.Products.Update;

namespace FreshFlow.Catalog.UnitTests.Products;

[Trait("Category", "Unit")]
public sealed class UpdateProductCommandValidatorTests
{
    private readonly UpdateProductCommandValidator _sut = new();

    [Fact]
    public async Task Validate_ValidCommand_Passes()
    {
        var cmd = new UpdateProductCommand(Guid.NewGuid(), "Cà rốt", Guid.NewGuid(), null, null);
        var result = await _sut.ValidateAsync(cmd);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_EmptyId_Fails()
    {
        var cmd = new UpdateProductCommand(Guid.Empty, "Cà rốt", Guid.NewGuid(), null, null);
        var result = await _sut.ValidateAsync(cmd);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Id");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Validate_EmptyName_Fails(string name)
    {
        var cmd = new UpdateProductCommand(Guid.NewGuid(), name, Guid.NewGuid(), null, null);
        var result = await _sut.ValidateAsync(cmd);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Name");
    }

    [Fact]
    public async Task Validate_EmptyUnitId_Fails()
    {
        var cmd = new UpdateProductCommand(Guid.NewGuid(), "Cà rốt", Guid.Empty, null, null);
        var result = await _sut.ValidateAsync(cmd);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "UnitId");
    }

    [Fact]
    public async Task Validate_EmptyGuidCategoryId_Fails()
    {
        var cmd = new UpdateProductCommand(Guid.NewGuid(), "Cà rốt", Guid.NewGuid(), Guid.Empty, null);
        var result = await _sut.ValidateAsync(cmd);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "CategoryId");
    }

    [Fact]
    public async Task Validate_ValidImageUrl_Passes()
    {
        var cmd = new UpdateProductCommand(
            Guid.NewGuid(), "Cà rốt", Guid.NewGuid(), null, null,
            ImageUrl: "https://res.cloudinary.com/demo/image/upload/v1/freshflow/products/abc.jpg");
        var result = await _sut.ValidateAsync(cmd);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_NullImageUrl_Passes()
    {
        var cmd = new UpdateProductCommand(Guid.NewGuid(), "Cà rốt", Guid.NewGuid(), null, null, ImageUrl: null);
        var result = await _sut.ValidateAsync(cmd);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_ImageUrlExceeds512Chars_Fails()
    {
        var cmd = new UpdateProductCommand(
            Guid.NewGuid(), "Cà rốt", Guid.NewGuid(), null, null,
            ImageUrl: "https://example.com/" + new string('x', 500));
        var result = await _sut.ValidateAsync(cmd);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "ImageUrl");
    }

    [Theory]
    [InlineData("not-a-url")]
    [InlineData("ftp://example.com/product.jpg")]
    public async Task Validate_InvalidImageUrl_Fails(string url)
    {
        var cmd = new UpdateProductCommand(Guid.NewGuid(), "Cà rốt", Guid.NewGuid(), null, null, ImageUrl: url);
        var result = await _sut.ValidateAsync(cmd);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "ImageUrl");
    }
}
