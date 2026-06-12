using FluentAssertions;
using FreshFlow.Pricing.Application.Abstractions;
using FreshFlow.Pricing.Application.Dtos;
using FreshFlow.Pricing.Application.Queries.GetMarketProducts;
using NSubstitute;

namespace FreshFlow.Pricing.UnitTests.Queries;

[Trait("Category", "Unit")]
public sealed class GetMarketProductsQueryHandlerTests
{
    private readonly IMarketProductReader _reader = Substitute.For<IMarketProductReader>();
    private readonly GetMarketProductsQueryHandler _sut;

    private static readonly Guid MarketId = Guid.NewGuid();

    public GetMarketProductsQueryHandlerTests()
    {
        _sut = new GetMarketProductsQueryHandler(_reader);
    }

    // ── Market validation ─────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_MarketNotFound_ReturnsNotFoundError()
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
    public async Task Handle_MarketNotFound_DoesNotCallGetPage()
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
    public async Task Handle_ValidMarket_ReturnsProductPage()
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
    public async Task Handle_ValidMarket_EmptyPage_ReturnsEmptyList()
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
    public async Task Handle_WithCursor_ForwardsAllParametersToReader()
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
    public async Task Handle_ReaderReturnsNextCursor_PageDtoHasCursor()
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
    public async Task Handle_PageSizeIsPreservedInResult()
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

    // ── Helpers ──────────────────────────────────────────────────────────────

    private void SetupMarketExists() =>
        _reader.MarketExistsAsync(MarketId, default).Returns(true);

    private static IReadOnlyList<MarketProductItemDto> BuildItems(int count) =>
        Enumerable.Range(0, count)
            .Select(_ => new MarketProductItemDto(
                Guid.NewGuid(), Guid.NewGuid(), MarketId,
                "Test Product", "thủy hải sản", "kg",
                100_000m, 50, 50, DateTime.UtcNow, null))
            .ToList()
            .AsReadOnly();
}
