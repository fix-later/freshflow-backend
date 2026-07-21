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
        var category = new ProductCategory("Rau củ");
        _categories.FindByIdAsync(category.Id, default).Returns(category);
        _categories.ExistsByNameAsync("Hải sản", default).Returns(false);

        var result = await _sut.Handle(new UpdateCategoryCommand(category.Id, "Hải sản"), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Hải sản");
        await _categories.Received(1).SaveChangesAsync(default);
    }

    [Fact]
    public async Task Handle_ReparentChild_UpdatesParent()
    {
        var category = new ProductCategory("Rau ăn lá", Guid.NewGuid());
        var newParent = new ProductCategory("Nông sản");
        _categories.FindByIdAsync(category.Id, default).Returns(category);
        _categories.FindByIdAsync(newParent.Id, default).Returns(newParent);

        var result = await _sut.Handle(
            new UpdateCategoryCommand(category.Id, category.Name, newParent.Id), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.ParentId.Should().Be(newParent.Id);
    }

    [Fact]
    public async Task Handle_PromoteChild_ClearsParent()
    {
        var category = new ProductCategory("Rau ăn lá", Guid.NewGuid());
        _categories.FindByIdAsync(category.Id, default).Returns(category);

        var result = await _sut.Handle(
            new UpdateCategoryCommand(category.Id, category.Name, ParentId: null), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.ParentId.Should().BeNull();
    }

    [Fact]
    public async Task Handle_DemoteRootWithoutChildren_SetsParent()
    {
        var category = new ProductCategory("Rau");
        var parent = new ProductCategory("Nông sản");
        _categories.FindByIdAsync(category.Id, default).Returns(category);
        _categories.FindByIdAsync(parent.Id, default).Returns(parent);
        _categories.HasChildrenAsync(category.Id, false, default).Returns(false);

        var result = await _sut.Handle(
            new UpdateCategoryCommand(category.Id, category.Name, parent.Id), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.ParentId.Should().Be(parent.Id);
    }

    [Fact]
    public async Task Handle_DemoteRootWithChildren_ReturnsInvalidParent()
    {
        var category = new ProductCategory("Rau");
        var parent = new ProductCategory("Nông sản");
        _categories.FindByIdAsync(category.Id, default).Returns(category);
        _categories.FindByIdAsync(parent.Id, default).Returns(parent);
        _categories.HasChildrenAsync(category.Id, false, default).Returns(true);

        var result = await _sut.Handle(
            new UpdateCategoryCommand(category.Id, category.Name, parent.Id), default);

        result.Error.Code.Should().Be("INVALID_CATEGORY_PARENT");
        await _categories.DidNotReceive().SaveChangesAsync(default);
    }

    [Fact]
    public async Task Handle_SelfParent_ReturnsInvalidParent()
    {
        var category = new ProductCategory("Rau");
        _categories.FindByIdAsync(category.Id, default).Returns(category);

        var result = await _sut.Handle(
            new UpdateCategoryCommand(category.Id, category.Name, category.Id), default);

        result.Error.Code.Should().Be("INVALID_CATEGORY_PARENT");
    }

    [Fact]
    public async Task Handle_MissingParent_ReturnsNotFound()
    {
        var category = new ProductCategory("Rau");
        var parentId = Guid.NewGuid();
        _categories.FindByIdAsync(category.Id, default).Returns(category);
        _categories.FindByIdAsync(parentId, default).Returns((ProductCategory?)null);

        var result = await _sut.Handle(
            new UpdateCategoryCommand(category.Id, category.Name, parentId), default);

        result.Error.Code.Should().Be("CATEGORY_PARENT_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_ChildParentThatWouldCreateCycle_ReturnsInvalidParent()
    {
        var category = new ProductCategory("Rau");
        var child = new ProductCategory("Rau ăn lá", category.Id);
        _categories.FindByIdAsync(category.Id, default).Returns(category);
        _categories.FindByIdAsync(child.Id, default).Returns(child);

        var result = await _sut.Handle(
            new UpdateCategoryCommand(category.Id, category.Name, child.Id), default);

        result.Error.Code.Should().Be("INVALID_CATEGORY_PARENT");
    }

    [Fact]
    public async Task Handle_UnchangedInactiveParent_AllowsRename()
    {
        var parent = new ProductCategory("Rau");
        parent.Deactivate();
        var category = new ProductCategory("Rau ăn lá", parent.Id);
        _categories.FindByIdAsync(category.Id, default).Returns(category);

        var result = await _sut.Handle(
            new UpdateCategoryCommand(category.Id, "Rau lá", parent.Id), default);

        result.IsSuccess.Should().BeTrue();
        await _categories.DidNotReceive().FindByIdAsync(parent.Id, default);
    }

    [Fact]
    public async Task Handle_SameName_SkipsDuplicateCheck()
    {
        var category = new ProductCategory("Gia vị");
        _categories.FindByIdAsync(category.Id, default).Returns(category);

        var result = await _sut.Handle(new UpdateCategoryCommand(category.Id, "Gia vị"), default);

        result.IsSuccess.Should().BeTrue();
        await _categories.DidNotReceive().ExistsByNameAsync(Arg.Any<string>(), default);
    }

    [Fact]
    public async Task Handle_DuplicateName_ReturnsConflict()
    {
        var category = new ProductCategory("Rau củ");
        _categories.FindByIdAsync(category.Id, default).Returns(category);
        _categories.ExistsByNameAsync("Thịt", default).Returns(true);

        var result = await _sut.Handle(new UpdateCategoryCommand(category.Id, "Thịt"), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("CATEGORY_NAME_CONFLICT");
        await _categories.DidNotReceive().SaveChangesAsync(default);
    }

    [Fact]
    public async Task Handle_NonExistentCategory_ReturnsNotFound()
    {
        var id = Guid.NewGuid();
        _categories.FindByIdAsync(id, default).Returns((ProductCategory?)null);

        var result = await _sut.Handle(new UpdateCategoryCommand(id, "Name"), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("CATEGORY_NOT_FOUND");
    }
}
