using FluentAssertions;
using FreshFlow.Catalog.Application.Abstractions;
using FreshFlow.Catalog.Application.Queries.Categories.GetCategoryById;
using FreshFlow.Catalog.Domain.Entities;
using NSubstitute;

namespace FreshFlow.Catalog.UnitTests.Categories;

[Trait("Category", "Unit")]
public sealed class GetCategoryByIdQueryHandlerTests
{
    private readonly IProductCategoryRepository _categories = Substitute.For<IProductCategoryRepository>();
    private readonly GetCategoryByIdQueryHandler _sut;

    public GetCategoryByIdQueryHandlerTests()
    {
        _sut = new GetCategoryByIdQueryHandler(_categories);
    }

    [Fact]
    public async Task Handle_ExistingCategory_ReturnsDto()
    {
        // Arrange
        var category = new ProductCategory("Hải sản");
        _categories.FindByIdAsync(category.Id, default).Returns(category);

        // Act
        var result = await _sut.Handle(new GetCategoryByIdQuery(category.Id), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(category.Id);
        result.Value.Name.Should().Be("Hải sản");
    }

    [Fact]
    public async Task Handle_NonExistentCategory_ReturnsNotFound()
    {
        // Arrange
        var id = Guid.NewGuid();
        _categories.FindByIdAsync(id, default).Returns((ProductCategory?)null);

        // Act
        var result = await _sut.Handle(new GetCategoryByIdQuery(id), default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("CATEGORY_NOT_FOUND");
    }
}
