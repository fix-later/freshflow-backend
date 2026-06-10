using FluentAssertions;
using FreshFlow.Catalog.Application.Abstractions;
using FreshFlow.Catalog.Application.Commands.Categories.Deactivate;
using FreshFlow.Catalog.Domain.Entities;
using NSubstitute;

namespace FreshFlow.Catalog.UnitTests.Categories;

[Trait("Category", "Unit")]
public sealed class DeactivateCategoryCommandHandlerTests
{
    private readonly IProductCategoryRepository _categories = Substitute.For<IProductCategoryRepository>();
    private readonly DeactivateCategoryCommandHandler _sut;

    public DeactivateCategoryCommandHandlerTests()
    {
        _sut = new DeactivateCategoryCommandHandler(_categories);
    }

    [Fact]
    public async Task Handle_ActiveCategory_DeactivatesAndReturnsDto()
    {
        // Arrange
        var category = new ProductCategory("Rau củ");
        category.IsActive.Should().BeTrue();
        _categories.FindByIdAsync(category.Id, default).Returns(category);

        // Act
        var result = await _sut.Handle(new DeactivateCategoryCommand(category.Id), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.IsActive.Should().BeFalse();
        await _categories.Received(1).SaveChangesAsync(default);
    }

    [Fact]
    public async Task Handle_NonExistentCategory_ReturnsNotFound()
    {
        // Arrange
        var id = Guid.NewGuid();
        _categories.FindByIdAsync(id, default).Returns((ProductCategory?)null);

        // Act
        var result = await _sut.Handle(new DeactivateCategoryCommand(id), default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("CATEGORY_NOT_FOUND");
        await _categories.DidNotReceive().SaveChangesAsync(default);
    }
}
