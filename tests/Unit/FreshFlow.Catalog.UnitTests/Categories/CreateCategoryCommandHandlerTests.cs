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
        _sut = new CreateCategoryCommandHandler(_categories);
    }

    [Fact]
    public async Task Handle_NewName_CreatesCategory()
    {
        // Arrange
        _categories.ExistsByNameAsync("Rau củ", default).Returns(false);
        var cmd = new CreateCategoryCommand("Rau củ");

        // Act
        var result = await _sut.Handle(cmd, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Rau củ");
        result.Value.IsActive.Should().BeTrue();
        await _categories.Received(1).AddAsync(Arg.Is<ProductCategory>(c => c.Name == "Rau củ"), default);
        await _categories.Received(1).SaveChangesAsync(default);
    }

    [Fact]
    public async Task Handle_DuplicateName_ReturnsConflict()
    {
        // Arrange
        _categories.ExistsByNameAsync("Thịt", default).Returns(true);

        // Act
        var result = await _sut.Handle(new CreateCategoryCommand("Thịt"), default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("CATEGORY_NAME_CONFLICT");
        await _categories.DidNotReceive().AddAsync(Arg.Any<ProductCategory>(), default);
    }
}
