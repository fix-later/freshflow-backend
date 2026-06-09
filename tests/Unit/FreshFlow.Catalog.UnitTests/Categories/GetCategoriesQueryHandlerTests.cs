using FluentAssertions;
using FreshFlow.Catalog.Application.Abstractions;
using FreshFlow.Catalog.Application.Queries.Categories.GetCategories;
using FreshFlow.Catalog.Domain.Entities;
using NSubstitute;

namespace FreshFlow.Catalog.UnitTests.Categories;

[Trait("Category", "Unit")]
public sealed class GetCategoriesQueryHandlerTests
{
    private readonly IProductCategoryRepository _categories = Substitute.For<IProductCategoryRepository>();
    private readonly GetCategoriesQueryHandler _sut;

    public GetCategoriesQueryHandlerTests()
    {
        _sut = new GetCategoriesQueryHandler(_categories);
    }

    [Fact]
    public async Task Handle_ActiveOnly_ReturnsActiveCategories()
    {
        // Arrange
        IReadOnlyList<ProductCategory> list =
        [
            new ProductCategory("Rau củ"),
            new ProductCategory("Thịt"),
        ];
        _categories.GetAllAsync(true, default).Returns(list);

        // Act
        var result = await _sut.Handle(new GetCategoriesQuery(ActiveOnly: true), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_IncludeInactive_DelegatesToRepository()
    {
        // Arrange
        IReadOnlyList<ProductCategory> list = [];
        _categories.GetAllAsync(false, default).Returns(list);

        // Act
        var result = await _sut.Handle(new GetCategoriesQuery(ActiveOnly: false), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _categories.Received(1).GetAllAsync(false, default);
    }
}
