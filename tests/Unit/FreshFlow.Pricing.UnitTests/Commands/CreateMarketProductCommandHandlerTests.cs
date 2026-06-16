using FluentAssertions;
using FreshFlow.Pricing.Application.Abstractions;
using FreshFlow.Pricing.Application.Commands.CreateMarketProduct;
using FreshFlow.Pricing.Domain.Entities;
using NSubstitute;
using NSubstitute.ReturnsExtensions;

namespace FreshFlow.Pricing.UnitTests.Commands;

[Trait("Category", "Unit")]
public sealed class CreateMarketProductCommandHandlerTests
{
    private readonly IMarketProductRepository _mpRepo =
        Substitute.For<IMarketProductRepository>();

    private readonly IMarketProductReader _marketProductReader =
        Substitute.For<IMarketProductReader>();

    private readonly CreateMarketProductCommandHandler _sut;

    private static readonly Guid AdminId = Guid.NewGuid();
    private static readonly Guid MarketId = Guid.NewGuid();
    private static readonly Guid ProductId = Guid.NewGuid();

    public CreateMarketProductCommandHandlerTests()
    {
        _sut = new CreateMarketProductCommandHandler(_mpRepo, _marketProductReader);

        // Defaults: market exists, product exists, no existing listing — override per test.
        _marketProductReader.MarketExistsAsync(MarketId, Arg.Any<CancellationToken>())
            .Returns(true);
        _marketProductReader.ProductExistsAsync(ProductId, Arg.Any<CancellationToken>())
            .Returns(true);
        _mpRepo.FindByMarketAndProductAsync(MarketId, ProductId, Arg.Any<CancellationToken>())
            .ReturnsNull();
    }

    // ── Helper ────────────────────────────────────────────────────────────────

    private static CreateMarketProductCommand Cmd(
        decimal initialPrice = 25_000m,
        int initialQuantity = 100,
        Guid? createdBy = null) =>
        new(MarketId, ProductId, initialPrice, initialQuantity, createdBy ?? AdminId);

    // ── 422 Business-rule violations ──────────────────────────────────────────

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100_000)]
    public async Task Handle_InvalidPrice_ReturnsInvalidPriceErrorAsync(decimal price)
    {
        // Arrange — price <= 0 is a 422 business rule, not a 400 structural error
        var cmd = Cmd(initialPrice: price);

        // Act
        var result = await _sut.Handle(cmd, default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("INVALID_PRICE");
        await _marketProductReader.DidNotReceive()
            .MarketExistsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-100)]
    public async Task Handle_InvalidQuantity_ReturnsInvalidQuantityErrorAsync(int quantity)
    {
        // Arrange — quantity < 0 is a 422 business rule
        var cmd = Cmd(initialQuantity: quantity);

        // Act
        var result = await _sut.Handle(cmd, default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("INVALID_QUANTITY");
        await _marketProductReader.DidNotReceive()
            .MarketExistsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_QuantityZero_IsValidAsync()
    {
        // Arrange — quantity=0 is valid (out-of-stock signal at listing time)
        var cmd = Cmd(initialQuantity: 0);

        // Act
        var result = await _sut.Handle(cmd, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    // ── 404 Market not found ──────────────────────────────────────────────────

    [Fact]
    public async Task Handle_MarketNotFound_ReturnsMarketNotFoundAsync()
    {
        // Arrange
        _marketProductReader.MarketExistsAsync(MarketId, Arg.Any<CancellationToken>())
            .Returns(false);

        // Act
        var result = await _sut.Handle(Cmd(), default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("MARKET_NOT_FOUND");
        await _marketProductReader.DidNotReceive()
            .ProductExistsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    // ── 404 Product not found ─────────────────────────────────────────────────

    [Fact]
    public async Task Handle_ProductNotFound_ReturnsProductNotFoundAsync()
    {
        // Arrange
        _marketProductReader.ProductExistsAsync(ProductId, Arg.Any<CancellationToken>())
            .Returns(false);

        // Act
        var result = await _sut.Handle(Cmd(), default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("PRODUCT_NOT_FOUND");
        await _mpRepo.DidNotReceive().FindByMarketAndProductAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    // ── 409 Duplicate listing ─────────────────────────────────────────────────

    [Fact]
    public async Task Handle_AlreadyListed_ReturnsConflictAsync()
    {
        // Arrange
        var existing = new MarketProduct(MarketId, ProductId, 10_000m, 50, null);
        _mpRepo.FindByMarketAndProductAsync(MarketId, ProductId, Arg.Any<CancellationToken>())
            .Returns(existing);

        // Act
        var result = await _sut.Handle(Cmd(), default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("MARKET_PRODUCT_ALREADY_EXISTS");
        await _mpRepo.DidNotReceive().AddAsync(
            Arg.Any<MarketProduct>(), Arg.Any<CancellationToken>());
    }

    // ── 201 Success ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_Success_ReturnsCorrectDtoAsync()
    {
        // Act
        var result = await _sut.Handle(Cmd(initialPrice: 30_000m, initialQuantity: 200), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var dto = result.Value;
        dto.MarketId.Should().Be(MarketId);
        dto.ProductId.Should().Be(ProductId);
        dto.CurrentPrice.Should().Be(30_000m);
        dto.CurrentQuantity.Should().Be(200);
        dto.CreatedBy.Should().Be(AdminId);
        dto.MarketProductId.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task Handle_Success_PersistsViaAddAndSaveChangesAsync()
    {
        // Act
        await _sut.Handle(Cmd(), default);

        // Assert
        await _mpRepo.Received(1).AddAsync(
            Arg.Is<MarketProduct>(mp =>
                mp.MarketId == MarketId &&
                mp.ProductId == ProductId &&
                mp.CurrentPrice == 25_000m &&
                mp.CurrentQuantity == 100),
            Arg.Any<CancellationToken>());
        await _mpRepo.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
