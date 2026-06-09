using FluentAssertions;
using FreshFlow.Catalog.Application.Abstractions;
using FreshFlow.Catalog.Application.Commands.Products.Deactivate;
using FreshFlow.Catalog.Domain.Entities;
using NSubstitute;

namespace FreshFlow.Catalog.UnitTests.Products;

[Trait("Category", "Unit")]
public sealed class DeactivateProductCommandHandlerTests
{
    private readonly IProductRepository _products = Substitute.For<IProductRepository>();
    private readonly DeactivateProductCommandHandler _sut;

    public DeactivateProductCommandHandlerTests()
    {
        _sut = new DeactivateProductCommandHandler(_products);
    }

    [Fact]
    public async Task Handle_ExistingProduct_SoftDeletesAndReturnsDto()
    {
        // Arrange
        var product = new Product("Cà rốt", Guid.NewGuid(), null, null, null);
        _products.FindByIdAsync(product.Id, default).Returns(product);

        // Act
        var result = await _sut.Handle(new DeactivateProductCommand(product.Id), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(product.Id);
        await _products.Received(1).SaveChangesAsync(default);
    }

    [Fact]
    public async Task Handle_ProductNotFound_ReturnsNotFound()
    {
        // Arrange
        var id = Guid.NewGuid();
        _products.FindByIdAsync(id, default).Returns((Product?)null);

        // Act
        var result = await _sut.Handle(new DeactivateProductCommand(id), default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("PRODUCT_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_ProductAlreadyDeactivated_ReturnsProductNotFound()
    {
        // Arrange — repository returns null because the product is soft-deleted
        // (FindByIdAsync filters DeletedAt == null, so a deleted product is invisible)
        var id = Guid.NewGuid();
        _products.FindByIdAsync(id, default).Returns((Product?)null);

        // Act
        var result = await _sut.Handle(new DeactivateProductCommand(id), default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("PRODUCT_NOT_FOUND");
    }
}
