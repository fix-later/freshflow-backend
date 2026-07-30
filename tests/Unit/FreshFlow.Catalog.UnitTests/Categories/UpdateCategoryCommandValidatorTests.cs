using FluentAssertions;
using FreshFlow.Catalog.Application.Commands.Categories.Update;

namespace FreshFlow.Catalog.UnitTests.Categories;

[Trait("Category", "Unit")]
public sealed class UpdateCategoryCommandValidatorTests
{
    private readonly UpdateCategoryCommandValidator _sut = new();

    [Fact]
    public async Task Validate_InvalidImageUrl_Fails()
    {
        var result = await _sut.ValidateAsync(
            new UpdateCategoryCommand(Guid.NewGuid(), "Rau củ", ImageUrl: "not-a-url"));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "ImageUrl");
    }
}
