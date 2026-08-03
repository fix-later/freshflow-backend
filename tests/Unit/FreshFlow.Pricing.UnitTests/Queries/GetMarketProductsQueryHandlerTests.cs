using FluentAssertions;
using FreshFlow.Pricing.Application.Abstractions;
using FreshFlow.Pricing.Application.Dtos;
using FreshFlow.Pricing.Application.Queries.GetMarketProducts;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace FreshFlow.Pricing.UnitTests.Queries;

[Trait("Category", "Unit")]
public sealed class GetMarketProductsQueryHandlerTests
{
    private readonly IMarketProductReader _reader = Substitute.For<IMarketProductReader>();
    private readonly IPriceBoardReader _priceBoardReader = Substitute.For<IPriceBoardReader>();
    private readonly GetMarketProductsQueryHandler _sut;

    private static readonly Guid MarketId = Guid.NewGuid();

    public GetMarketProductsQueryHandlerTests()
    {
        // Default price board: all cache miss → items returned unchanged from DB.
        _priceBoardReader
            .GetBatchAsync(Arg.Any<Guid>(), Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, LivePriceEntry>());

        _sut = new GetMarketProductsQueryHandler(
            _reader,
            _priceBoardReader,
            Substitute.For<ILogger<GetMarketProductsQueryHandler>>());
    }

    // ── Market validation ─────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_MarketNotFound_ReturnsNotFoundErrorAsync()
    {
        // Arrange
        _reader.MarketExistsAsync(MarketId, default).Returns(false);

        // Act
        var result = await _sut.Handle(new GetMarketProductsQuery(MarketId), default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("MARKET_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_MarketNotFound_DoesNotCallGetPageAsync()
    {
        // Arrange
        _reader.MarketExistsAsync(MarketId, default).Returns(false);

        // Act
        await _sut.Handle(new GetMarketProductsQuery(MarketId), default);

        // Assert — short-circuit: page query must NOT be called
        await _reader.DidNotReceive()
            .GetPageAsync(Arg.Any<Guid>(), Arg.Any<string?>(), Arg.Any<string?>(),
                Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    // ── Success path ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_ValidMarket_ReturnsProductPageAsync()
    {
        // Arrange
        SetupMarketExists();
        var items = BuildItems(2);
        _reader.GetPageAsync(MarketId, null, null, 20, default).Returns((items, (string?)null));

        // Act
        var result = await _sut.Handle(new GetMarketProductsQuery(MarketId), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(2);
        result.Value.PageSize.Should().Be(20);
        result.Value.NextCursor.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ValidMarket_EmptyPage_ReturnsEmptyListAsync()
    {
        // Arrange
        SetupMarketExists();
        _reader.GetPageAsync(MarketId, null, null, 20, default)
            .Returns((Array.Empty<MarketProductItemDto>() as IReadOnlyList<MarketProductItemDto>, (string?)null));

        // Act
        var result = await _sut.Handle(new GetMarketProductsQuery(MarketId), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WithCursor_ForwardsAllParametersToReaderAsync()
    {
        // Arrange
        SetupMarketExists();
        const string cursor = "some-cursor";
        const string category = "thủy hải sản";
        const int pageSize = 50;
        _reader.GetPageAsync(MarketId, category, cursor, pageSize, default)
            .Returns((Array.Empty<MarketProductItemDto>() as IReadOnlyList<MarketProductItemDto>, (string?)null));

        // Act
        await _sut.Handle(
            new GetMarketProductsQuery(MarketId, category, cursor, pageSize), default);

        // Assert
        await _reader.Received(1)
            .GetPageAsync(MarketId, category, cursor, pageSize, default);
    }

    [Fact]
    public async Task Handle_ReaderReturnsNextCursor_PageDtoHasCursorAsync()
    {
        // Arrange
        SetupMarketExists();
        const string nextCursor = "next-cursor-value";
        var items = BuildItems(20);
        _reader.GetPageAsync(MarketId, null, null, 20, default).Returns((items, nextCursor));

        // Act
        var result = await _sut.Handle(new GetMarketProductsQuery(MarketId), default);

        // Assert
        result.Value.NextCursor.Should().Be(nextCursor);
    }

    [Fact]
    public async Task Handle_PageSizeIsPreservedInResultAsync()
    {
        // Arrange
        SetupMarketExists();
        _reader.GetPageAsync(MarketId, null, null, 50, default)
            .Returns((Array.Empty<MarketProductItemDto>() as IReadOnlyList<MarketProductItemDto>, (string?)null));

        // Act
        var result = await _sut.Handle(new GetMarketProductsQuery(MarketId, PageSize: 50), default);

        // Assert
        result.Value.PageSize.Should().Be(50);
    }

    // ── UC-PRI-09: live price board overlay ───────────────────────────────────

    [Fact]
    public async Task Handle_PriceBoardHit_OverridesPriceFromDbAsync()
    {
        // Arrange
        SetupMarketExists();
        var product = BuildItem(currentPrice: 100_000m, currentQuantity: 50);
        _reader.GetPageAsync(MarketId, null, null, 20, default)
            .Returns((new[] { product } as IReadOnlyList<MarketProductItemDto>, (string?)null));

        _priceBoardReader
            .GetBatchAsync(Arg.Any<Guid>(), Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, LivePriceEntry>
            {
                [product.ProductId] = new LivePriceEntry(Price: 125_000m, Quantity: 30, AvailableQuantity: 30)
            });

        // Act
        var result = await _sut.Handle(new GetMarketProductsQuery(MarketId), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Items[0].CurrentPrice.Should().Be(125_000m,
            "price board entry takes precedence over DB price");
        result.Value.Items[0].CurrentQuantity.Should().Be(30,
            "price board quantity overrides DB quantity");
        result.Value.Items[0].AvailableQuantity.Should().Be(30,
            "price board availableQuantity (= Quantity for v1) overrides DB value");
    }

    [Fact]
    public async Task Handle_PriceBoardMiss_KeepsDbPriceAsync()
    {
        // Arrange — price board returns empty dict (all miss)
        SetupMarketExists();
        var product = BuildItem(currentPrice: 100_000m, currentQuantity: 50);
        _reader.GetPageAsync(MarketId, null, null, 20, default)
            .Returns((new[] { product } as IReadOnlyList<MarketProductItemDto>, (string?)null));

        // Default mock already returns empty dict

        // Act
        var result = await _sut.Handle(new GetMarketProductsQuery(MarketId), default);

        // Assert
        result.Value.Items[0].CurrentPrice.Should().Be(100_000m,
            "DB price kept when price board has no entry for this product");
        result.Value.Items[0].AvailableQuantity.Should().Be(50,
            "DB availableQuantity kept on cache miss");
    }

    [Fact]
    public async Task Handle_PartialPriceBoardHit_MixesLiveAndDbPricesAsync()
    {
        // Arrange
        SetupMarketExists();
        var p1 = BuildItem(currentPrice: 100_000m, currentQuantity: 50);
        var p2 = BuildItem(currentPrice: 200_000m, currentQuantity: 100);
        _reader.GetPageAsync(MarketId, null, null, 20, default)
            .Returns((new[] { p1, p2 } as IReadOnlyList<MarketProductItemDto>, (string?)null));

        // Only p1 has a price board entry
        _priceBoardReader
            .GetBatchAsync(Arg.Any<Guid>(), Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, LivePriceEntry>
            {
                [p1.ProductId] = new LivePriceEntry(Price: 115_000m, Quantity: 40, AvailableQuantity: 40)
            });

        // Act
        var result = await _sut.Handle(new GetMarketProductsQuery(MarketId), default);

        // Assert
        result.Value.Items.Should().HaveCount(2);
        result.Value.Items.First(i => i.ProductId == p1.ProductId).CurrentPrice
            .Should().Be(115_000m, "price board hit for p1");
        result.Value.Items.First(i => i.ProductId == p2.ProductId).CurrentPrice
            .Should().Be(200_000m, "DB fallback for p2 (no board entry)");
    }

    [Fact]
    public async Task Handle_PriceBoardThrows_DoesNotRethrowAsync()
    {
        // Arrange
        SetupMarketExists();
        var items = BuildItems(1);
        _reader.GetPageAsync(MarketId, null, null, 20, default).Returns((items, (string?)null));
        _priceBoardReader
            .GetBatchAsync(Arg.Any<Guid>(), Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new Exception("Price board unavailable"));

        // Act
        var act = () => _sut.Handle(new GetMarketProductsQuery(MarketId), default);

        // Assert — price board failure must NOT propagate
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Handle_PriceBoardThrows_FallsBackToDbPricesAsync()
    {
        // Arrange
        SetupMarketExists();
        var product = BuildItem(currentPrice: 99_000m, currentQuantity: 20);
        _reader.GetPageAsync(MarketId, null, null, 20, default)
            .Returns((new[] { product } as IReadOnlyList<MarketProductItemDto>, (string?)null));
        _priceBoardReader
            .GetBatchAsync(Arg.Any<Guid>(), Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new Exception("Price board unavailable"));

        // Act
        var result = await _sut.Handle(new GetMarketProductsQuery(MarketId), default);

        // Assert — DB values preserved when price board throws
        result.IsSuccess.Should().BeTrue();
        result.Value.Items[0].CurrentPrice.Should().Be(99_000m);
        result.Value.Items[0].CurrentQuantity.Should().Be(20);
    }

    [Fact]
    public async Task Handle_EmptyPage_PriceBoardNotCalledAsync()
    {
        // Arrange
        SetupMarketExists();
        _reader.GetPageAsync(MarketId, null, null, 20, default)
            .Returns((Array.Empty<MarketProductItemDto>() as IReadOnlyList<MarketProductItemDto>, (string?)null));

        // Act
        await _sut.Handle(new GetMarketProductsQuery(MarketId), default);

        // Assert — no point querying price board when there are no products
        await _priceBoardReader.DidNotReceive()
            .GetBatchAsync(Arg.Any<Guid>(), Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PriceBoardCalledWithCorrectMarketAndProductIdsAsync()
    {
        // Arrange
        SetupMarketExists();
        var p1 = BuildItem();
        var p2 = BuildItem();
        _reader.GetPageAsync(MarketId, null, null, 20, default)
            .Returns((new[] { p1, p2 } as IReadOnlyList<MarketProductItemDto>, (string?)null));

        // Act
        await _sut.Handle(new GetMarketProductsQuery(MarketId), default);

        // Assert
        await _priceBoardReader.Received(1).GetBatchAsync(
            Arg.Is<Guid>(id => id == MarketId),
            Arg.Is<IReadOnlyList<Guid>>(ids =>
                ids.Count == 2 &&
                ids.Contains(p1.ProductId) &&
                ids.Contains(p2.ProductId)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_DbMetadataAlwaysPreserved_EvenOnCacheHitAsync()
    {
        // Arrange — price board provides live price but name/unit/category must stay from DB
        SetupMarketExists();
        var product = BuildItem(currentPrice: 100_000m, currentQuantity: 50);
        _reader.GetPageAsync(MarketId, null, null, 20, default)
            .Returns((new[] { product } as IReadOnlyList<MarketProductItemDto>, (string?)null));

        _priceBoardReader
            .GetBatchAsync(Arg.Any<Guid>(), Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, LivePriceEntry>
            {
                [product.ProductId] = new LivePriceEntry(Price: 125_000m, Quantity: 30, AvailableQuantity: 30)
            });

        // Act
        var result = await _sut.Handle(new GetMarketProductsQuery(MarketId), default);

        // Assert — only price fields change; identity and metadata are unchanged
        var item = result.Value.Items[0];
        item.MarketProductId.Should().Be(product.MarketProductId);
        item.ProductId.Should().Be(product.ProductId);
        item.ProductName.Should().Be("Test Product");
        item.Category.Should().Be("thủy hải sản");
        item.Unit.Should().Be("kg");
        item.SellingUnit.Should().Be(new SellingUnitDto("Carton", 10));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void SetupMarketExists() =>
        _reader.MarketExistsAsync(MarketId, default).Returns(true);

    private static MarketProductItemDto BuildItem(
        decimal currentPrice = 100_000m,
        int currentQuantity = 50) =>
        new(
            MarketProductId: Guid.NewGuid(),
            ProductId: Guid.NewGuid(),
            MarketId: MarketId,
            ProductName: "Test Product",
            Category: "thủy hải sản",
            Unit: "kg",
            CurrentPrice: currentPrice,
            CurrentQuantity: currentQuantity,
            AvailableQuantity: currentQuantity,
            UpdatedAt: DateTime.UtcNow,
            UpdatedBy: null,
            SellingUnit: new SellingUnitDto("Carton", 10));

    private static IReadOnlyList<MarketProductItemDto> BuildItems(int count) =>
        Enumerable.Range(0, count)
            .Select(_ => BuildItem())
            .ToList()
            .AsReadOnly();
}
