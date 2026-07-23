using FluentAssertions;
using FreshFlow.Catalog.Application.Abstractions;
using FreshFlow.Catalog.Application.Commands.Products.Update;
using FreshFlow.Catalog.Domain.Entities;
using NSubstitute;

namespace FreshFlow.Catalog.UnitTests.Products;

[Trait("Category", "Unit")]
public sealed class UpdateProductCommandHandlerTests
{
    private readonly IProductRepository _products = Substitute.For<IProductRepository>();
    private readonly IProductCategoryRepository _categories = Substitute.For<IProductCategoryRepository>();
    private readonly IUnitOfMeasurementRepository _units = Substitute.For<IUnitOfMeasurementRepository>();
    private readonly IPackingCodeRepository _packingCodes = Substitute.For<IPackingCodeRepository>();
    private readonly UpdateProductCommandHandler _sut;

    public UpdateProductCommandHandlerTests()
    {
        _sut = new UpdateProductCommandHandler(_products, _categories, _units, _packingCodes);
    }

    [Fact]
    public async Task Handle_ValidUpdate_ReturnsUpdatedDto()
    {
        // Arrange
        var unit = new UnitOfMeasurement("kg", "kg");
        var product = new Product("Cà rốt cũ", unit.Id, null, null, null);
        _products.FindByIdAsync(product.Id, default).Returns(product);
        _units.FindByIdAsync(unit.Id, default).Returns(unit);

        var cmd = new UpdateProductCommand(product.Id, "Cà rốt mới", unit.Id, null, "Mô tả mới");

        // Act
        var result = await _sut.Handle(cmd, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Cà rốt mới");
        result.Value.Description.Should().Be("Mô tả mới");
        await _products.Received(1).SaveChangesAsync(default);
    }

    [Fact]
    public async Task Handle_ProductNotFound_ReturnsNotFound()
    {
        // Arrange
        var id = Guid.NewGuid();
        _products.FindByIdAsync(id, default).Returns((Product?)null);

        // Act
        var result = await _sut.Handle(
            new UpdateProductCommand(id, "x", Guid.NewGuid(), null, null), default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("PRODUCT_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_InvalidUnit_ReturnsInvalidUnit()
    {
        // Arrange
        var unit = new UnitOfMeasurement("kg", "kg");
        var product = new Product("Cà rốt", unit.Id, null, null, null);
        _products.FindByIdAsync(product.Id, default).Returns(product);
        _units.FindByIdAsync(Arg.Any<Guid>(), default).Returns((UnitOfMeasurement?)null);

        // Act
        var result = await _sut.Handle(
            new UpdateProductCommand(product.Id, "Cà rốt", Guid.NewGuid(), null, null), default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("INVALID_UNIT");
    }

    [Fact]
    public async Task Handle_InvalidCategory_ReturnsInvalidCategory()
    {
        // Arrange
        var unit = new UnitOfMeasurement("kg", "kg");
        var product = new Product("Cà rốt", unit.Id, null, null, null);
        _products.FindByIdAsync(product.Id, default).Returns(product);
        _units.FindByIdAsync(unit.Id, default).Returns(unit);
        _categories.FindByIdAsync(Arg.Any<Guid>(), default).Returns((ProductCategory?)null);

        var categoryId = Guid.NewGuid();

        // Act
        var result = await _sut.Handle(
            new UpdateProductCommand(product.Id, "Cà rốt", unit.Id, categoryId, null), default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("INVALID_CATEGORY");
    }

    [Fact]
    public async Task Handle_ValidUpdateWithImageUrl_ReturnsImageUrlInDto()
    {
        // Arrange
        var unit = new UnitOfMeasurement("kg", "kg");
        var product = new Product("Cà rốt", unit.Id, null, null, null);
        _products.FindByIdAsync(product.Id, default).Returns(product);
        _units.FindByIdAsync(unit.Id, default).Returns(unit);

        var cmd = new UpdateProductCommand(
            product.Id, "Cà rốt", unit.Id, null, null,
            ImageUrl: "https://res.cloudinary.com/demo/image/upload/v1/freshflow/products/abc.jpg");

        // Act
        var result = await _sut.Handle(cmd, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ImageUrl.Should().Be(
            "https://res.cloudinary.com/demo/image/upload/v1/freshflow/products/abc.jpg");
    }

    [Fact]
    public async Task Handle_UpdateWithoutImageUrl_PreservesExistingImageUrl()
    {
        // Arrange
        var unit = new UnitOfMeasurement("kg", "kg");
        var product = new Product("Carrot", unit.Id, null, null, null);
        product.Update(
            product.Name,
            product.CategoryId,
            product.UnitId,
            product.Description,
            imageUrl: "https://res.cloudinary.com/demo/image/upload/v1/freshflow/products/old.jpg");

        _products.FindByIdAsync(product.Id, default).Returns(product);
        _units.FindByIdAsync(unit.Id, default).Returns(unit);

        var cmd = new UpdateProductCommand(product.Id, "Carrot updated", unit.Id, null, "Updated description");

        // Act
        var result = await _sut.Handle(cmd, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ImageUrl.Should().Be(
            "https://res.cloudinary.com/demo/image/upload/v1/freshflow/products/old.jpg");
    }

    [Fact]
    public async Task Handle_UpdateLegacyFields_ReflectsNewCategoryAndUnitNames()
    {
        // Arrange
        var unit = new UnitOfMeasurement("thùng", "thùng");
        var category = new ProductCategory("Hải sản");
        var product = new Product("Cá thu", unit.Id, null, null, null);
        _products.FindByIdAsync(product.Id, default).Returns(product);
        _units.FindByIdAsync(unit.Id, default).Returns(unit);
        _categories.FindByIdAsync(category.Id, default).Returns(category);

        // Act
        var result = await _sut.Handle(
            new UpdateProductCommand(product.Id, "Cá thu", unit.Id, category.Id, null), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.CategoryName.Should().Be("Hải sản");
        result.Value.UnitName.Should().Be("thùng");
    }
}
