using FluentAssertions;
using FreshFlow.Pricing.Application.Abstractions;
using FreshFlow.Pricing.Application.Commands.UpdateAvailableQuantity;
using FreshFlow.Pricing.Domain.Entities;
using NSubstitute;
using NSubstitute.ReturnsExtensions;

namespace FreshFlow.Pricing.UnitTests.Commands;

[Trait("Category", "Unit")]
public sealed class UpdateAvailableQuantityCommandHandlerTests
{
    private readonly IMarketProductRepository _mpRepo =
        Substitute.For<IMarketProductRepository>();

    private readonly IPriceSnapshotRepository _snapRepo =
        Substitute.For<IPriceSnapshotRepository>();

    private readonly IAssignedMarketReader _reader =
        Substitute.For<IAssignedMarketReader>();

    private readonly IMarketProductReader _marketProductReader =
        Substitute.For<IMarketProductReader>();

    private readonly UpdateAvailableQuantityCommandHandler _sut;

    private static readonly Guid AgentId = Guid.NewGuid();
    private static readonly Guid MarketId = Guid.NewGuid();
    private static readonly Guid ProductId = Guid.NewGuid();

    public UpdateAvailableQuantityCommandHandlerTests()
    {
        _sut = new UpdateAvailableQuantityCommandHandler(
            _mpRepo, _snapRepo, _reader, _marketProductReader);

        // Default: market exists — override per test when needed
        _marketProductReader.MarketExistsAsync(MarketId, Arg.Any<CancellationToken>())
            .Returns(true);
    }

    // ── Helper ────────────────────────────────────────────────────────────────

    private static UpdateAvailableQuantityCommand Cmd(
        int quantity = 300,
        DateTime? expectedVersion = null) =>
        new(MarketId, ProductId, AgentId, quantity, expectedVersion);

    private static MarketProduct MakeProduct(
        decimal initialPrice = 100_000m, int initialQty = 200) =>
        new(MarketId, ProductId, initialPrice, initialQty, null);

    // ── 422 Business-rule violations ──────────────────────────────────────────

    [Theory]
    [InlineData(-1)]
    [InlineData(-100)]
    public async Task Handle_NegativeQuantity_ReturnsInvalidQuantityErrorAsync(int quantity)
    {
        // Arrange — quantity < 0 is a 422 business rule
        var cmd = Cmd(quantity: quantity);

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
        // Arrange — override default
        _marketProductReader.MarketExistsAsync(MarketId, Arg.Any<CancellationToken>())
            .Returns(false);

        // Act
        var result = await _sut.Handle(Cmd(quantity: 100), default);

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
        // Arrange
        _reader.HasAssignmentAsync(AgentId, MarketId, default).Returns(false);

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
        _mpRepo.FindByMarketAndProductAsync(MarketId, ProductId, default).ReturnsNull();

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
        var wrongVersion = mp.UpdatedAt.AddSeconds(-1);

        _reader.HasAssignmentAsync(AgentId, MarketId, default).Returns(true);
        _mpRepo.FindByMarketAndProductAsync(MarketId, ProductId, default).Returns(mp);

        // Act
        var result = await _sut.Handle(Cmd(expectedVersion: wrongVersion), default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("OPTIMISTIC_CONCURRENCY_CONFLICT");
    }

    [Fact]
    public async Task Handle_NoExpectedVersion_SkipsConcurrencyCheckAsync()
    {
        // Arrange
        var mp = MakeProduct();
        _reader.HasAssignmentAsync(AgentId, MarketId, default).Returns(true);
        _mpRepo.FindByMarketAndProductAsync(MarketId, ProductId, default).Returns(mp);

        // Act
        var result = await _sut.Handle(Cmd(quantity: 300, expectedVersion: null), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    // ── 200 Quantity update ───────────────────────────────────────────────────

    [Fact]
    public async Task Handle_ValidQuantity_ReturnsCorrectDtoAsync()
    {
        // Arrange
        var mp = MakeProduct(initialPrice: 100_000m, initialQty: 200);
        _reader.HasAssignmentAsync(AgentId, MarketId, default).Returns(true);
        _mpRepo.FindByMarketAndProductAsync(MarketId, ProductId, default).Returns(mp);

        // Act
        var result = await _sut.Handle(Cmd(quantity: 350), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var dto = result.Value;
        dto.PreviousQuantity.Should().Be(200);
        dto.CurrentQuantity.Should().Be(350);
        dto.IsOutOfStock.Should().BeFalse();
        dto.MarketId.Should().Be(MarketId);
        dto.ProductId.Should().Be(ProductId);
        dto.UpdatedBy.Should().Be(AgentId);
    }

    [Fact]
    public async Task Handle_ZeroQuantity_SetsIsOutOfStockTrueAsync()
    {
        // Arrange
        var mp = MakeProduct(initialQty: 200);
        _reader.HasAssignmentAsync(AgentId, MarketId, default).Returns(true);
        _mpRepo.FindByMarketAndProductAsync(MarketId, ProductId, default).Returns(mp);

        // Act
        var result = await _sut.Handle(Cmd(quantity: 0), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.CurrentQuantity.Should().Be(0);
        result.Value.IsOutOfStock.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ValidQuantity_PriceIsNotChangedAsync()
    {
        // Arrange — quantity-only endpoint must not alter price
        var mp = MakeProduct(initialPrice: 100_000m, initialQty: 50);
        _reader.HasAssignmentAsync(AgentId, MarketId, default).Returns(true);
        _mpRepo.FindByMarketAndProductAsync(MarketId, ProductId, default).Returns(mp);

        // Act
        await _sut.Handle(Cmd(quantity: 300), default);

        // Assert
        mp.CurrentPrice.Should().Be(100_000m);
    }

    [Fact]
    public async Task Handle_ValidQuantity_TracksAndSavesAsync()
    {
        // Arrange
        var mp = MakeProduct();
        _reader.HasAssignmentAsync(AgentId, MarketId, default).Returns(true);
        _mpRepo.FindByMarketAndProductAsync(MarketId, ProductId, default).Returns(mp);

        // Act
        await _sut.Handle(Cmd(quantity: 400), default);

        // Assert
        _mpRepo.Received(1).Track(mp);
        await _mpRepo.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ValidQuantity_PersistsSnapshotViaForFactoryAsync()
    {
        // Arrange — FR-PRI-004: snapshot is created on every price OR quantity change
        var mp = MakeProduct(initialPrice: 100_000m, initialQty: 50);
        _reader.HasAssignmentAsync(AgentId, MarketId, default).Returns(true);
        _mpRepo.FindByMarketAndProductAsync(MarketId, ProductId, default).Returns(mp);

        // Act
        await _sut.Handle(Cmd(quantity: 400), default);

        // Assert — PriceSnapshot.For(mp, actor): price unchanged, qty=new qty
        await _snapRepo.Received(1).AddAsync(
            Arg.Is<PriceSnapshot>(s =>
                s.MarketProductId == mp.Id &&
                s.Price == 100_000m &&
                s.Quantity == 400 &&
                s.RecordedBy == AgentId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ExpectedVersionMatches_SucceedsAsync()
    {
        // Arrange
        var mp = MakeProduct();
        var correctVersion = mp.UpdatedAt;

        _reader.HasAssignmentAsync(AgentId, MarketId, default).Returns(true);
        _mpRepo.FindByMarketAndProductAsync(MarketId, ProductId, default).Returns(mp);

        // Act
        var result = await _sut.Handle(Cmd(quantity: 300, expectedVersion: correctVersion), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    // ── Fix #4: DateTime kind normalization ───────────────────────────────────

    [Fact]
    public async Task Handle_ExpectedVersionMatchesButKindIsUnspecified_TreatsAsEquivalentToUtcAsync()
    {
        // Arrange — simulate the JSON deserialization bug: client sends "2026-06-14T07:00:00"
        // without a timezone suffix, which System.Text.Json deserializes as DateTimeKind.Unspecified,
        // while EF Core returns the same instant as DateTimeKind.Utc. Without normalization these
        // two DateTimes would compare as unequal even though they represent the same moment.
        var mp = MakeProduct();
        var utcVersion = mp.UpdatedAt;                                        // DateTimeKind.Utc (EF)
        var unspecifiedVersion = DateTime.SpecifyKind(utcVersion, DateTimeKind.Unspecified); // JSON kind

        _reader.HasAssignmentAsync(AgentId, MarketId, default).Returns(true);
        _mpRepo.FindByMarketAndProductAsync(MarketId, ProductId, default).Returns(mp);

        // Act
        var result = await _sut.Handle(Cmd(quantity: 300, expectedVersion: unspecifiedVersion), default);

        // Assert — must succeed (no spurious 409 conflict)
        result.IsSuccess.Should().BeTrue(
            "Unspecified kind with the same ticks as a Utc datetime should be treated as equal");
    }

    [Fact]
    public async Task Handle_ExpectedVersionWithLocalKindSameInstant_TreatsAsEquivalentToUtcAsync()
    {
        // Arrange — DateTimeKind.Local with the same UTC instant (on UTC servers ToUniversalTime is noop)
        var mp = MakeProduct();
        var utcVersion = mp.UpdatedAt;
        var localVersion = DateTime.SpecifyKind(utcVersion, DateTimeKind.Local);

        _reader.HasAssignmentAsync(AgentId, MarketId, default).Returns(true);
        _mpRepo.FindByMarketAndProductAsync(MarketId, ProductId, default).Returns(mp);

        // Act
        var result = await _sut.Handle(Cmd(quantity: 300, expectedVersion: localVersion), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }
}
