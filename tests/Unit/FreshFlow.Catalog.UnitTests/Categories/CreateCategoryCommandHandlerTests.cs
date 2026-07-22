using FluentAssertions;
using FreshFlow.Catalog.Application.Abstractions;
using FreshFlow.Catalog.Application.Commands.Categories.Create;
using FreshFlow.Catalog.Domain.Entities;
using NSubstitute;

namespace FreshFlow.Catalog.UnitTests.Categories;

[Trait("Category", "Unit")]
public sealed class CreateCategoryCommandHandlerTests
{
    private readonly IProductCategoryRepository _categories = Substitute.For<IProductCategoryRepository>();
    private readonly CreateCategoryCommandHandler _sut;

    public CreateCategoryCommandHandlerTests()
    {
        _categories.SaveChangesAsync(default).Returns(true);
        _sut = new CreateCategoryCommandHandler(_categories);
    }

    [Fact]
    public async Task Handle_NewName_CreatesRootCategory()
    {
        _categories.ExistsByNameAsync("Rau củ", default).Returns(false);

        var result = await _sut.Handle(new CreateCategoryCommand("Rau củ"), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Rau củ");
        result.Value.ParentId.Should().BeNull();
        result.Value.IsActive.Should().BeTrue();
        await _categories.Received(1).AddAsync(
            Arg.Is<ProductCategory>(c => c.Name == "Rau củ" && c.ParentId == null), default);
        await _categories.Received(1).SaveChangesAsync(default);
    }

    [Fact]
    public async Task Handle_ValidParent_CreatesChildCategory()
    {
        var parent = new ProductCategory("Rau");
        _categories.FindByIdAsync(parent.Id, default).Returns(parent);

        var result = await _sut.Handle(new CreateCategoryCommand("Rau ăn lá", parent.Id), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.ParentId.Should().Be(parent.Id);
        await _categories.Received(1).AddAsync(
            Arg.Is<ProductCategory>(c => c.ParentId == parent.Id), default);
    }

    [Fact]
    public async Task Handle_MissingParent_ReturnsNotFound()
    {
        var parentId = Guid.NewGuid();
        _categories.FindByIdAsync(parentId, default).Returns((ProductCategory?)null);

        var result = await _sut.Handle(new CreateCategoryCommand("Rau ăn lá", parentId), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("CATEGORY_PARENT_NOT_FOUND");
        await _categories.DidNotReceive().AddAsync(Arg.Any<ProductCategory>(), default);
    }

    [Fact]
    public async Task Handle_InactiveParent_ReturnsInvalidParent()
    {
        var parent = new ProductCategory("Rau");
        parent.Deactivate();
        _categories.FindByIdAsync(parent.Id, default).Returns(parent);

        var result = await _sut.Handle(new CreateCategoryCommand("Rau ăn lá", parent.Id), default);

        result.Error.Code.Should().Be("INVALID_CATEGORY_PARENT");
    }

    [Fact]
    public async Task Handle_ChildAsParent_ReturnsInvalidParent()
    {
        var child = new ProductCategory("Rau ăn lá", Guid.NewGuid());
        _categories.FindByIdAsync(child.Id, default).Returns(child);

        var result = await _sut.Handle(new CreateCategoryCommand("Rau cải", child.Id), default);

        result.Error.Code.Should().Be("INVALID_CATEGORY_PARENT");
    }

    [Fact]
    public async Task Handle_DuplicateName_ReturnsConflict()
    {
        _categories.ExistsByNameAsync("Thịt", default).Returns(true);

        var result = await _sut.Handle(new CreateCategoryCommand("Thịt"), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("CATEGORY_NAME_CONFLICT");
        await _categories.DidNotReceive().AddAsync(Arg.Any<ProductCategory>(), default);
    }
}
