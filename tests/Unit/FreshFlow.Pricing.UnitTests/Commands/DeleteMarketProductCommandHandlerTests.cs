using FluentAssertions;
using FreshFlow.Pricing.Application.Abstractions;
using FreshFlow.Pricing.Application.Commands.DeleteMarketProduct;
using FreshFlow.Pricing.Domain.Entities;
using NSubstitute;
using NSubstitute.ReturnsExtensions;

namespace FreshFlow.Pricing.UnitTests.Commands;

[Trait("Category", "Unit")]
public sealed class DeleteMarketProductCommandHandlerTests
{
    private readonly IMarketProductRepository _mpRepo = Substitute.For<IMarketProductRepository>();
    private readonly DeleteMarketProductCommandHandler _sut;

    private static readonly Guid MarketId = Guid.NewGuid();
    private static readonly Guid ProductId = Guid.NewGuid();

    public DeleteMarketProductCommandHandlerTests()
    {
        _sut = new DeleteMarketProductCommandHandler(_mpRepo);
    }

    private static MarketProduct MakeProduct(int initialQty = 200) =>
        new(MarketId, ProductId, 100_000m, initialQty, null);

    /// <summary>
    /// ReservedQuantity is only ever mutated by Orders' cross-module SQL (never through the
    /// domain), so tests reach it via reflection on the private-set property.
    /// </summary>
    private static MarketProduct WithReserved(MarketProduct mp, int reserved)
    {
        typeof(MarketProduct).GetProperty(nameof(MarketProduct.ReservedQuantity))!
            .SetValue(mp, reserved);
        return mp;
    }

    [Fact]
    public async Task Handle_ProductNotFound_ReturnsNotFoundAsync()
    {
        // Arrange
        _mpRepo.FindByMarketAndProductAsync(MarketId, ProductId, default).ReturnsNull();

        // Act
        var result = await _sut.Handle(new DeleteMarketProductCommand(MarketId, ProductId), default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("MARKET_PRODUCT_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_ReservedQuantityPositive_ReturnsMarketProductHasReservedStockAsync()
    {
        // Arrange — 10 units reserved by confirmed orders
        var mp = WithReserved(MakeProduct(), 10);
        _mpRepo.FindByMarketAndProductAsync(MarketId, ProductId, default).Returns(mp);

        // Act
        var result = await _sut.Handle(new DeleteMarketProductCommand(MarketId, ProductId), default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("MARKET_PRODUCT_HAS_RESERVED_STOCK");
        mp.IsDeleted.Should().BeFalse();
        await _mpRepo.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ReservedQuantityZero_SoftDeletesAndSucceedsAsync()
    {
        // Arrange
        var mp = MakeProduct();
        _mpRepo.FindByMarketAndProductAsync(MarketId, ProductId, default).Returns(mp);

        // Act
        var result = await _sut.Handle(new DeleteMarketProductCommand(MarketId, ProductId), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        mp.IsDeleted.Should().BeTrue();
        _mpRepo.Received(1).Track(mp);
        await _mpRepo.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
