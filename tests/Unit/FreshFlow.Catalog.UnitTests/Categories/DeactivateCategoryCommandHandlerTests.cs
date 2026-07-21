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
    public async Task Handle_CategoryWithoutActiveChildren_DeactivatesAndReturnsDto()
    {
        var category = new ProductCategory("Rau củ");
        _categories.FindByIdAsync(category.Id, default).Returns(category);
        _categories.HasChildrenAsync(category.Id, true, default).Returns(false);

        var result = await _sut.Handle(new DeactivateCategoryCommand(category.Id), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.IsActive.Should().BeFalse();
        await _categories.Received(1).SaveChangesAsync(default);
    }

    [Fact]
    public async Task Handle_CategoryWithActiveChildren_ReturnsConflict()
    {
        var category = new ProductCategory("Rau củ");
        _categories.FindByIdAsync(category.Id, default).Returns(category);
        _categories.HasChildrenAsync(category.Id, true, default).Returns(true);

        var result = await _sut.Handle(new DeactivateCategoryCommand(category.Id), default);

        result.Error.Code.Should().Be("CATEGORY_HAS_ACTIVE_CHILDREN");
        await _categories.DidNotReceive().SaveChangesAsync(default);
    }

    [Fact]
    public async Task Handle_NonExistentCategory_ReturnsNotFound()
    {
        var id = Guid.NewGuid();
        _categories.FindByIdAsync(id, default).Returns((ProductCategory?)null);

        var result = await _sut.Handle(new DeactivateCategoryCommand(id), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("CATEGORY_NOT_FOUND");
        await _categories.DidNotReceive().SaveChangesAsync(default);
    }
}
