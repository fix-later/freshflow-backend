using FluentAssertions;
using FreshFlow.Pricing.Application.Abstractions;
using FreshFlow.Pricing.Application.Commands.UpdateProductPrice;
using FreshFlow.Pricing.Domain.Entities;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using NSubstitute.ReturnsExtensions;

namespace FreshFlow.Pricing.UnitTests.Commands;

[Trait("Category", "Unit")]
public sealed class UpdateProductPriceCommandHandlerTests
{
    private readonly IMarketProductRepository _mpRepo =
        Substitute.For<IMarketProductRepository>();

    private readonly IPriceSnapshotRepository _snapRepo =
        Substitute.For<IPriceSnapshotRepository>();

    private readonly IAssignedMarketReader _reader =
        Substitute.For<IAssignedMarketReader>();

    private readonly IMarketProductReader _marketProductReader =
        Substitute.For<IMarketProductReader>();

    private readonly UpdateProductPriceCommandHandler _sut;

    private static readonly Guid AgentId = Guid.NewGuid();
    private static readonly Guid MarketId = Guid.NewGuid();
    private static readonly Guid ProductId = Guid.NewGuid();

    public UpdateProductPriceCommandHandlerTests()
    {
        _sut = new UpdateProductPriceCommandHandler(
            _mpRepo, _snapRepo, _reader, _marketProductReader);

        // Default: market exists — override per test when needed
        _marketProductReader.MarketExistsAsync(MarketId, Arg.Any<CancellationToken>())
            .Returns(true);
    }

    // ── Helper ────────────────────────────────────────────────────────────────

    private static UpdateProductPriceCommand Cmd(
        decimal? price = 135_000m,
        int? quantity = null,
        DateTime? expectedVersion = null) =>
        new(MarketId, ProductId, AgentId, price, quantity, expectedVersion);

    private static MarketProduct MakeProduct(
        decimal initialPrice = 125_000m, int initialQty = 500) =>
        new(MarketId, ProductId, initialPrice, initialQty, null);

    // ── 422 Business-rule violations ──────────────────────────────────────────

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100_000)]
    public async Task Handle_InvalidPrice_ReturnsInvalidPriceErrorAsync(decimal price)
    {
        // Arrange — price ≤ 0 is a 422 business rule, not a 400 structural error
        var cmd = Cmd(price: price, quantity: null);

        // Act
        var result = await _sut.Handle(cmd, default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("INVALID_PRICE");
        // No DB call should be made
        await _marketProductReader.DidNotReceive()
            .MarketExistsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-100)]
    public async Task Handle_InvalidQuantity_ReturnsInvalidQuantityErrorAsync(int quantity)
    {
        // Arrange — quantity < 0 is a 422 business rule
        var cmd = Cmd(price: null, quantity: quantity);

        // Act
        var result = await _sut.Handle(cmd, default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("INVALID_QUANTITY");
        // No DB call should be made
        await _marketProductReader.DidNotReceive()
            .MarketExistsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    // ── 404 Market not found ──────────────────────────────────────────────────

    [Fact]
    public async Task Handle_MarketNotFound_ReturnsMarketNotFoundAsync()
    {
        // Arrange — override default: market does not exist
        _marketProductReader.MarketExistsAsync(MarketId, Arg.Any<CancellationToken>())
            .Returns(false);

        // Act
        var result = await _sut.Handle(Cmd(price: 100_000m), default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("MARKET_NOT_FOUND");
        await _reader.DidNotReceive()
            .HasAssignmentAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    // ── 403 Assignment guard ──────────────────────────────────────────────────

    [Fact]
    public async Task Handle_AgentNotAssigned_ReturnsMarketAccessDeniedAsync()
    {
        // Arrange — agent has no assignments
        _reader.HasAssignmentAsync(AgentId, MarketId, default)
            .Returns(false);

        // Act
        var result = await _sut.Handle(Cmd(), default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("MARKET_ACCESS_DENIED");
        await _mpRepo.DidNotReceive().FindByMarketAndProductAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    // ── 404 Product not found ─────────────────────────────────────────────────

    [Fact]
    public async Task Handle_ProductNotFound_ReturnsProductNotFoundAsync()
    {
        // Arrange
        _reader.HasAssignmentAsync(AgentId, MarketId, default).Returns(true);
        _mpRepo.FindByMarketAndProductAsync(MarketId, ProductId, default)
            .ReturnsNull();

        // Act
        var result = await _sut.Handle(Cmd(), default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("PRODUCT_NOT_FOUND");
    }

    // ── 409 Optimistic concurrency ────────────────────────────────────────────

    [Fact]
    public async Task Handle_ExpectedVersionMismatch_ReturnsConflictAsync()
    {
        // Arrange
        var mp = MakeProduct();
        var wrongVersion = mp.UpdatedAt.AddSeconds(-1); // stale version

        _reader.HasAssignmentAsync(AgentId, MarketId, default).Returns(true);
        _mpRepo.FindByMarketAndProductAsync(MarketId, ProductId, default).Returns(mp);

        var cmd = Cmd(expectedVersion: wrongVersion);

        // Act
        var result = await _sut.Handle(cmd, default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("OPTIMISTIC_CONCURRENCY_CONFLICT");
    }

    [Fact]
    public async Task Handle_NoExpectedVersion_SkipsConcurrencyCheckAsync()
    {
        // Arrange — cmd has no expectedVersion → should NOT conflict
        var mp = MakeProduct();
        _reader.HasAssignmentAsync(AgentId, MarketId, default).Returns(true);
        _mpRepo.FindByMarketAndProductAsync(MarketId, ProductId, default).Returns(mp);

        // Act
        var result = await _sut.Handle(Cmd(price: 135_000m, expectedVersion: null), default);

        // Assert
        result.IsSuccess.Should().BeTrue("no expectedVersion means no concurrency check");
    }

    // ── 200 Price update ──────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_PriceUpdateOnly_ReturnsCorrectDtoAsync()
    {
        // Arrange
        var mp = MakeProduct(initialPrice: 100_000m, initialQty: 200);
        _reader.HasAssignmentAsync(AgentId, MarketId, default).Returns(true);
        _mpRepo.FindByMarketAndProductAsync(MarketId, ProductId, default).Returns(mp);

        var cmd = Cmd(price: 120_000m, quantity: null);

        // Act
        var result = await _sut.Handle(cmd, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var dto = result.Value;
        dto.PreviousPrice.Should().Be(100_000m);
        dto.CurrentPrice.Should().Be(120_000m);
        dto.CurrentQuantity.Should().Be(200); // unchanged
        dto.ChangePercent.Should().Be(20.00m); // (120000-100000)/100000*100
        dto.MarketId.Should().Be(MarketId);
        dto.ProductId.Should().Be(ProductId);
        dto.UpdatedBy.Should().Be(AgentId);
    }

    [Fact]
    public async Task Handle_PriceUpdate_TracksAndSavesMarketProductAsync()
    {
        // Arrange
        var mp = MakeProduct();
        _reader.HasAssignmentAsync(AgentId, MarketId, default).Returns(true);
        _mpRepo.FindByMarketAndProductAsync(MarketId, ProductId, default).Returns(mp);

        // Act
        await _sut.Handle(Cmd(price: 135_000m), default);

        // Assert — Track called, SaveChanges called once
        _mpRepo.Received(1).Track(mp);
        await _mpRepo.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PriceUpdate_PersistsSnapshotViaForFactoryAsync()
    {
        // Arrange
        var mp = MakeProduct(initialPrice: 125_000m, initialQty: 500);
        _reader.HasAssignmentAsync(AgentId, MarketId, default).Returns(true);
        _mpRepo.FindByMarketAndProductAsync(MarketId, ProductId, default).Returns(mp);

        // Act
        await _sut.Handle(Cmd(price: 135_000m), default);

        // Assert — snapshot created via PriceSnapshot.For(mp, actor): price=new price, qty=current qty
        await _snapRepo.Received(1).AddAsync(
            Arg.Is<PriceSnapshot>(s =>
                s.MarketProductId == mp.Id &&
                s.Price == 135_000m &&
                s.Quantity == 500 &&
                s.RecordedBy == AgentId),
            Arg.Any<CancellationToken>());
    }

    // ── 200 Quantity-only update ──────────────────────────────────────────────

    [Fact]
    public async Task Handle_QuantityOnlyUpdate_PriceUnchangedAndChangePercentZeroAsync()
    {
        // Arrange
        var mp = MakeProduct(initialPrice: 100_000m, initialQty: 200);
        _reader.HasAssignmentAsync(AgentId, MarketId, default).Returns(true);
        _mpRepo.FindByMarketAndProductAsync(MarketId, ProductId, default).Returns(mp);

        var cmd = Cmd(price: null, quantity: 300);

        // Act
        var result = await _sut.Handle(cmd, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var dto = result.Value;
        dto.PreviousPrice.Should().Be(100_000m);
        dto.CurrentPrice.Should().Be(100_000m); // price unchanged
        dto.CurrentQuantity.Should().Be(300);   // quantity updated
        dto.ChangePercent.Should().Be(0m);
    }

    // ── 200 Both price and quantity ───────────────────────────────────────────

    [Fact]
    public async Task Handle_BothPriceAndQuantity_UpdatesBothFieldsAsync()
    {
        // Arrange
        var mp = MakeProduct(initialPrice: 100_000m, initialQty: 200);
        _reader.HasAssignmentAsync(AgentId, MarketId, default).Returns(true);
        _mpRepo.FindByMarketAndProductAsync(MarketId, ProductId, default).Returns(mp);

        var cmd = Cmd(price: 110_000m, quantity: 300);

        // Act
        var result = await _sut.Handle(cmd, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var dto = result.Value;
        dto.CurrentPrice.Should().Be(110_000m);
        dto.CurrentQuantity.Should().Be(300);
        dto.ChangePercent.Should().Be(10.00m);
    }

    // ── SnapshotId in result ──────────────────────────────────────────────────

    [Fact]
    public async Task Handle_Success_SnapshotIdIsNonEmptyAsync()
    {
        // Arrange
        var mp = MakeProduct();
        _reader.HasAssignmentAsync(AgentId, MarketId, default).Returns(true);
        _mpRepo.FindByMarketAndProductAsync(MarketId, ProductId, default).Returns(mp);

        // Act
        var result = await _sut.Handle(Cmd(price: 135_000m), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.SnapshotId.Should().NotBe(Guid.Empty);
    }

    // ── Optimistic concurrency happy path ─────────────────────────────────────

    [Fact]
    public async Task Handle_ExpectedVersionMatches_SucceedsAsync()
    {
        // Arrange
        var mp = MakeProduct();
        var correctVersion = mp.UpdatedAt; // matches current UpdatedAt

        _reader.HasAssignmentAsync(AgentId, MarketId, default).Returns(true);
        _mpRepo.FindByMarketAndProductAsync(MarketId, ProductId, default).Returns(mp);

        var cmd = Cmd(price: 135_000m, expectedVersion: correctVersion);

        // Act
        var result = await _sut.Handle(cmd, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    // ── 409 DB-level concurrency (EF token fires at SaveChanges) ─────────────

    [Fact]
    public async Task Handle_SaveChangesThrowsConcurrencyConflict_Returns409ConflictAsync()
    {
        // Arrange — pre-check passes, but the DB's concurrency token fires on SaveChanges
        // (simulates two agents submitting the same product update concurrently).
        var mp = MakeProduct();
        _reader.HasAssignmentAsync(AgentId, MarketId, default).Returns(true);
        _mpRepo.FindByMarketAndProductAsync(MarketId, ProductId, default).Returns(mp);
        _mpRepo.SaveChangesAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new ConcurrencyConflictException());

        // Act
        var result = await _sut.Handle(Cmd(price: 135_000m), default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("OPTIMISTIC_CONCURRENCY_CONFLICT");
    }
}
