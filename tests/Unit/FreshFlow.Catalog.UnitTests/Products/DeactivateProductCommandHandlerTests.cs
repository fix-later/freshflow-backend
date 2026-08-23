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
    private readonly IMarketListingReader _listings = Substitute.For<IMarketListingReader>();
    private readonly DeactivateProductCommandHandler _sut;

    public DeactivateProductCommandHandlerTests()
    {
        _sut = new DeactivateProductCommandHandler(_products, _listings);

        // Default: no active listings — override per test when needed
        _listings.CountActiveListingsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(0);
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
    public async Task Handle_ProductHasActiveListings_ReturnsProductHasActiveListings()
    {
        // Arrange
        var product = new Product("Cà rốt", Guid.NewGuid(), null, null, null);
        _products.FindByIdAsync(product.Id, default).Returns(product);
        _listings.CountActiveListingsAsync(product.Id, default).Returns(2);

        // Act
        var result = await _sut.Handle(new DeactivateProductCommand(product.Id), default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("PRODUCT_HAS_ACTIVE_LISTINGS");
        await _products.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ProductHasNoActiveListings_SucceedsAsync()
    {
        // Arrange
        var product = new Product("Cà rốt", Guid.NewGuid(), null, null, null);
        _products.FindByIdAsync(product.Id, default).Returns(product);
        _listings.CountActiveListingsAsync(product.Id, default).Returns(0);

        // Act
        var result = await _sut.Handle(new DeactivateProductCommand(product.Id), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
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
