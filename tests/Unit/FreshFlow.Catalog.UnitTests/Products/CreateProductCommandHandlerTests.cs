using FluentAssertions;
using FreshFlow.Catalog.Application.Abstractions;
using FreshFlow.Catalog.Application.Commands.Products.Create;
using FreshFlow.Catalog.Domain.Entities;
using NSubstitute;

namespace FreshFlow.Catalog.UnitTests.Products;

[Trait("Category", "Unit")]
public sealed class CreateProductCommandHandlerTests
{
    private readonly IProductRepository _products = Substitute.For<IProductRepository>();
    private readonly IProductCategoryRepository _categories = Substitute.For<IProductCategoryRepository>();
    private readonly IUnitOfMeasurementRepository _units = Substitute.For<IUnitOfMeasurementRepository>();
    private readonly IPackingCodeRepository _packingCodes = Substitute.For<IPackingCodeRepository>();
    private readonly CreateProductCommandHandler _sut;

    private static UnitOfMeasurement ActiveUnit(string name = "kg") =>
        new(name, name);

    private static ProductCategory ActiveCategory(string name = "Rau củ") =>
        new(name);

    public CreateProductCommandHandlerTests()
    {
        _sut = new CreateProductCommandHandler(_products, _categories, _units, _packingCodes);
    }

    [Fact]
    public async Task Handle_ValidWithCategory_ReturnsDto()
    {
        // Arrange
        var unit = ActiveUnit();
        var category = ActiveCategory();
        _units.FindByIdAsync(unit.Id, default).Returns(unit);
        _categories.FindByIdAsync(category.Id, default).Returns(category);

        var cmd = new CreateProductCommand("Cà rốt", unit.Id, category.Id, "Mô tả", null);

        // Act
        var result = await _sut.Handle(cmd, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Cà rốt");
        result.Value.UnitId.Should().Be(unit.Id);
        result.Value.CategoryId.Should().Be(category.Id);
        result.Value.UnitName.Should().Be("kg");
        result.Value.CategoryName.Should().Be("Rau củ");
        await _products.Received(1).AddAsync(Arg.Any<Product>(), default);
        await _products.Received(1).SaveChangesAsync(default);
    }

    [Fact]
    public async Task Handle_ValidNoCategoryId_ReturnsDto()
    {
        // Arrange
        var unit = ActiveUnit();
        _units.FindByIdAsync(unit.Id, default).Returns(unit);

        var cmd = new CreateProductCommand("Muối", unit.Id, null, null, null);

        // Act
        var result = await _sut.Handle(cmd, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.CategoryId.Should().BeNull();
    }

    [Fact]
    public async Task Handle_InactiveUnit_ReturnsInvalidUnit()
    {
        // Arrange — unit not found (null = not active/not found)
        _units.FindByIdAsync(Arg.Any<Guid>(), default).Returns((UnitOfMeasurement?)null);

        var cmd = new CreateProductCommand("Cà rốt", Guid.NewGuid(), null, null, null);

        // Act
        var result = await _sut.Handle(cmd, default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("INVALID_UNIT");
        await _products.DidNotReceive().AddAsync(Arg.Any<Product>(), default);
    }

    [Fact]
    public async Task Handle_InactiveCategory_ReturnsInvalidCategory()
    {
        // Arrange
        var unit = ActiveUnit();
        _units.FindByIdAsync(unit.Id, default).Returns(unit);
        _categories.FindByIdAsync(Arg.Any<Guid>(), default).Returns((ProductCategory?)null);

        var categoryId = Guid.NewGuid();
        var cmd = new CreateProductCommand("Cà rốt", unit.Id, categoryId, null, null);

        // Act
        var result = await _sut.Handle(cmd, default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("INVALID_CATEGORY");
    }

    [Fact]
    public async Task Handle_DeactivatedUnit_ReturnsInvalidUnit()
    {
        // Arrange — unit exists but is deactivated
        var unit = ActiveUnit();
        unit.Deactivate();
        _units.FindByIdAsync(unit.Id, default).Returns(unit);

        var cmd = new CreateProductCommand("Cà rốt", unit.Id, null, null, null);

        // Act
        var result = await _sut.Handle(cmd, default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("INVALID_UNIT");
    }
}
