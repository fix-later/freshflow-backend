using FluentAssertions;
using FreshFlow.Catalog.Application.Abstractions;
using FreshFlow.Catalog.Application.Commands.Categories.Activate;
using FreshFlow.Catalog.Domain.Entities;
using NSubstitute;

namespace FreshFlow.Catalog.UnitTests.Categories;

[Trait("Category", "Unit")]
public sealed class ActivateCategoryCommandHandlerTests
{
    private readonly IProductCategoryRepository _categories = Substitute.For<IProductCategoryRepository>();
    private readonly ActivateCategoryCommandHandler _sut;

    public ActivateCategoryCommandHandlerTests()
    {
        _sut = new ActivateCategoryCommandHandler(_categories);
    }

    [Fact]
    public async Task Handle_RootCategory_ActivatesAndReturnsDto()
    {
        var category = new ProductCategory("Rau củ");
        category.Deactivate();
        _categories.FindByIdAsync(category.Id, default).Returns(category);

        var result = await _sut.Handle(new ActivateCategoryCommand(category.Id), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.IsActive.Should().BeTrue();
        await _categories.Received(1).SaveChangesAsync(default);
    }

    [Fact]
    public async Task Handle_ActiveParent_ActivatesAndReturnsDto()
    {
        var parent = new ProductCategory("Thực phẩm");
        var category = new ProductCategory("Rau củ", parent.Id);
        category.Deactivate();
        _categories.FindByIdAsync(category.Id, default).Returns(category);
        _categories.FindByIdAsync(parent.Id, default).Returns(parent);

        var result = await _sut.Handle(new ActivateCategoryCommand(category.Id), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.IsActive.Should().BeTrue();
        await _categories.Received(1).SaveChangesAsync(default);
    }

    [Fact]
    public async Task Handle_InactiveParent_ReturnsValidationError()
    {
        var parent = new ProductCategory("Thực phẩm");
        parent.Deactivate();
        var category = new ProductCategory("Rau củ", parent.Id);
        _categories.FindByIdAsync(category.Id, default).Returns(category);
        _categories.FindByIdAsync(parent.Id, default).Returns(parent);

        var result = await _sut.Handle(new ActivateCategoryCommand(category.Id), default);

        result.Error.Code.Should().Be("INVALID_CATEGORY_PARENT");
        await _categories.DidNotReceive().SaveChangesAsync(default);
    }

    [Fact]
    public async Task Handle_ParentSoftDeleted_ReturnsValidationError()
    {
        var parentId = Guid.NewGuid();
        var category = new ProductCategory("Rau củ", parentId);
        _categories.FindByIdAsync(category.Id, default).Returns(category);
        _categories.FindByIdAsync(parentId, default).Returns((ProductCategory?)null);

        var result = await _sut.Handle(new ActivateCategoryCommand(category.Id), default);

        result.Error.Code.Should().Be("INVALID_CATEGORY_PARENT");
        await _categories.DidNotReceive().SaveChangesAsync(default);
    }

    [Fact]
    public async Task Handle_NonExistentCategory_ReturnsNotFound()
    {
        var id = Guid.NewGuid();
        _categories.FindByIdAsync(id, default).Returns((ProductCategory?)null);

        var result = await _sut.Handle(new ActivateCategoryCommand(id), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("CATEGORY_NOT_FOUND");
        await _categories.DidNotReceive().SaveChangesAsync(default);
    }
}
