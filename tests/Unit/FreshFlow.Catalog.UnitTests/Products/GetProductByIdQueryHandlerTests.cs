using FluentAssertions;
using FreshFlow.Catalog.Application.Abstractions;
using FreshFlow.Catalog.Application.Dtos;
using FreshFlow.Catalog.Application.Queries.Products.GetProductById;
using FreshFlow.Catalog.Domain.Entities;
using NSubstitute;

namespace FreshFlow.Catalog.UnitTests.Products;

[Trait("Category", "Unit")]
public sealed class GetProductByIdQueryHandlerTests
{
    private readonly IProductRepository _products = Substitute.For<IProductRepository>();
    private readonly GetProductByIdQueryHandler _sut;

    public GetProductByIdQueryHandlerTests()
    {
        _sut = new GetProductByIdQueryHandler(_products);
    }

    [Fact]
    public async Task Handle_ExistingProduct_ReturnsProductDto()
    {
        // Arrange
        var product = new Product("Cá lóc", Guid.NewGuid(), null, null, null);
        _products.FindByIdAsync(product.Id, default).Returns(product);

        // Act
        var result = await _sut.Handle(new GetProductByIdQuery(product.Id), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(product.Id);
        result.Value.Name.Should().Be("Cá lóc");
        result.Value.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ProductNotFound_ReturnsNotFound()
    {
        // Arrange
        var id = Guid.NewGuid();
        _products.FindByIdAsync(id, default).Returns((Product?)null);

        // Act
        var result = await _sut.Handle(new GetProductByIdQuery(id), default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("PRODUCT_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_SoftDeletedProduct_ReturnsNotFound()
    {
        // Arrange — repository returns null for soft-deleted products (DeletedAt filter in SQL)
        var id = Guid.NewGuid();
        _products.FindByIdAsync(id, default).Returns((Product?)null);

        // Act
        var result = await _sut.Handle(new GetProductByIdQuery(id), default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("PRODUCT_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_ProductWithCategoryAndUnit_PopulatesLegacyFields()
    {
        // Arrange
        var categoryId = Guid.NewGuid();
        var unitId = Guid.NewGuid();
        var product = new Product("Cà rốt", unitId, categoryId, "Mô tả", null,
            legacyCategory: "Rau củ", legacyUnit: "kg");
        _products.FindByIdAsync(product.Id, default).Returns(product);

        // Act
        var result = await _sut.Handle(new GetProductByIdQuery(product.Id), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.CategoryId.Should().Be(categoryId);
        result.Value.UnitId.Should().Be(unitId);
        result.Value.CategoryName.Should().Be("Rau củ");
        result.Value.UnitName.Should().Be("kg");
        result.Value.SellingUnit.Should().Be(new SellingUnitDto("kg", null));
        result.Value.Description.Should().Be("Mô tả");
    }
}
