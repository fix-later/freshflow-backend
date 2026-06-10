using FluentAssertions;
using FreshFlow.Catalog.Application.Abstractions;
using FreshFlow.Catalog.Application.Commands.Categories.Update;
using FreshFlow.Catalog.Domain.Entities;
using NSubstitute;

namespace FreshFlow.Catalog.UnitTests.Categories;

[Trait("Category", "Unit")]
public sealed class UpdateCategoryCommandHandlerTests
{
    private readonly IProductCategoryRepository _categories = Substitute.For<IProductCategoryRepository>();
    private readonly UpdateCategoryCommandHandler _sut;

    public UpdateCategoryCommandHandlerTests()
    {
        _sut = new UpdateCategoryCommandHandler(_categories);
    }

    [Fact]
    public async Task Handle_ValidRename_UpdatesCategory()
    {
        // Arrange
        var category = new ProductCategory("Rau củ");
        _categories.FindByIdAsync(category.Id, default).Returns(category);
        _categories.ExistsByNameAsync("Hải sản", default).Returns(false);

        // Act
        var result = await _sut.Handle(new UpdateCategoryCommand(category.Id, "Hải sản"), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Hải sản");
        await _categories.Received(1).SaveChangesAsync(default);
    }

    [Fact]
    public async Task Handle_SameName_SkipsDuplicateCheck()
    {
        // Arrange
        var category = new ProductCategory("Gia vị");
        _categories.FindByIdAsync(category.Id, default).Returns(category);

        // Act
        var result = await _sut.Handle(new UpdateCategoryCommand(category.Id, "Gia vị"), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _categories.DidNotReceive().ExistsByNameAsync(Arg.Any<string>(), default);
    }

    [Fact]
    public async Task Handle_DuplicateName_ReturnsConflict()
    {
        // Arrange
        var category = new ProductCategory("Rau củ");
        _categories.FindByIdAsync(category.Id, default).Returns(category);
        _categories.ExistsByNameAsync("Thịt", default).Returns(true);

        // Act
        var result = await _sut.Handle(new UpdateCategoryCommand(category.Id, "Thịt"), default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("CATEGORY_NAME_CONFLICT");
        await _categories.DidNotReceive().SaveChangesAsync(default);
    }

    [Fact]
    public async Task Handle_NonExistentCategory_ReturnsNotFound()
    {
        // Arrange
        var id = Guid.NewGuid();
        _categories.FindByIdAsync(id, default).Returns((ProductCategory?)null);

        // Act
        var result = await _sut.Handle(new UpdateCategoryCommand(id, "Name"), default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("CATEGORY_NOT_FOUND");
    }
}
