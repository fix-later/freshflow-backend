using FluentAssertions;
using FreshFlow.Pricing.Application.Abstractions;
using FreshFlow.Pricing.Application.Queries.GetPriceChangeHistory;
using FreshFlow.Pricing.Domain.Entities;
using NSubstitute;

namespace FreshFlow.Pricing.UnitTests.Queries;

[Trait("Category", "Unit")]
public sealed class GetPriceChangeHistoryQueryHandlerTests
{
    private readonly IMarketProductReader _marketReader =
        Substitute.For<IMarketProductReader>();

    private readonly IMarketProductRepository _marketProductRepo =
        Substitute.For<IMarketProductRepository>();

    private readonly IPriceSnapshotRepository _snapshotRepo =
        Substitute.For<IPriceSnapshotRepository>();

    private readonly GetPriceChangeHistoryQueryHandler _sut;

    private static readonly Guid MarketId = Guid.NewGuid();
    private static readonly Guid ProductId = Guid.NewGuid();

    public GetPriceChangeHistoryQueryHandlerTests()
    {
        // Default: empty snapshot page.
        _snapshotRepo
            .GetPageAsync(
                Arg.Any<Guid>(),
                Arg.Any<string?>(),
                Arg.Any<int>(),
                Arg.Any<DateTime?>(),
                Arg.Any<DateTime?>(),
                Arg.Any<CancellationToken>())
            .Returns((
                Array.Empty<PriceSnapshot>() as IReadOnlyList<PriceSnapshot>,
                (string?)null));

        _sut = new GetPriceChangeHistoryQueryHandler(
            _marketReader, _marketProductRepo, _snapshotRepo);
    }

    // ── Market validation (404 MARKET_NOT_FOUND) ──────────────────────────────

    [Fact]
    public async Task Handle_MarketNotFound_ReturnsMarketNotFoundError()
    {
        // Arrange
        _marketReader.MarketExistsAsync(MarketId, default).Returns(false);

        // Act
        var result = await _sut.Handle(
            new GetPriceChangeHistoryQuery(MarketId, ProductId), default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("MARKET_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_MarketNotFound_DoesNotQueryMarketProduct()
    {
        // Arrange
        _marketReader.MarketExistsAsync(MarketId, default).Returns(false);

        // Act
        await _sut.Handle(new GetPriceChangeHistoryQuery(MarketId, ProductId), default);

        // Assert — short-circuit after market check
        await _marketProductRepo.DidNotReceive()
            .FindByMarketAndProductAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    // ── Product validation (404 PRODUCT_NOT_FOUND) ────────────────────────────

    [Fact]
    public async Task Handle_ProductNotListedAtMarket_ReturnsProductNotFoundError()
    {
        // Arrange
        _marketReader.MarketExistsAsync(MarketId, default).Returns(true);
        _marketProductRepo
            .FindByMarketAndProductAsync(MarketId, ProductId, default)
            .Returns((MarketProduct?)null);

        // Act
        var result = await _sut.Handle(
            new GetPriceChangeHistoryQuery(MarketId, ProductId), default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("PRODUCT_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_ProductNotFound_DoesNotCallSnapshotRepo()
    {
        // Arrange
        _marketReader.MarketExistsAsync(MarketId, default).Returns(true);
        _marketProductRepo
            .FindByMarketAndProductAsync(MarketId, ProductId, default)
            .Returns((MarketProduct?)null);

        // Act
        await _sut.Handle(new GetPriceChangeHistoryQuery(MarketId, ProductId), default);

        // Assert — short-circuit after product check
        await _snapshotRepo.DidNotReceive()
            .GetPageAsync(
                Arg.Any<Guid>(),
                Arg.Any<string?>(),
                Arg.Any<int>(),
                Arg.Any<DateTime?>(),
                Arg.Any<DateTime?>(),
                Arg.Any<CancellationToken>());
    }

    // ── Success path ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_ValidRequest_ReturnsSuccess()
    {
        // Arrange
        SetupHappyPath();

        // Act
        var result = await _sut.Handle(
            new GetPriceChangeHistoryQuery(MarketId, ProductId), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_EmptyHistory_ReturnsEmptyPage()
    {
        // Arrange — default mock returns empty list
        SetupHappyPath();

        // Act
        var result = await _sut.Handle(
            new GetPriceChangeHistoryQuery(MarketId, ProductId), default);

        // Assert
        result.Value.Items.Should().BeEmpty();
        result.Value.NextCursor.Should().BeNull();
    }

    [Fact]
    public async Task Handle_UsesMarketProductIdFromResolvedMarketProduct()
    {
        // Arrange — capture the auto-generated Id from the created MarketProduct
        var mp = SetupHappyPath();

        // Act
        await _sut.Handle(new GetPriceChangeHistoryQuery(MarketId, ProductId), default);

        // Assert — snapshot query uses the resolved mp.Id, not ProductId directly
        await _snapshotRepo.Received(1)
            .GetPageAsync(
                Arg.Is<Guid>(id => id == mp.Id),
                Arg.Any<string?>(),
                Arg.Any<int>(),
                Arg.Any<DateTime?>(),
                Arg.Any<DateTime?>(),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithCursor_ForwardsCursorToSnapshotRepo()
    {
        // Arrange
        SetupHappyPath();
        const string cursor = "eyJpZCI6IjAwMDAwMDAwLTAwMDAtMDAwMC0wMDAwLTAwMDAwMDAwMDAwMCIsInJlY29yZGVkQXQiOiIyMDI2LTA2LTEzVDEwOjAwOjAwLjAwMDAwMDBaIn0=";

        // Act
        await _sut.Handle(
            new GetPriceChangeHistoryQuery(MarketId, ProductId, Cursor: cursor), default);

        // Assert
        await _snapshotRepo.Received(1)
            .GetPageAsync(
                Arg.Any<Guid>(),
                cursor,
                Arg.Any<int>(),
                Arg.Any<DateTime?>(),
                Arg.Any<DateTime?>(),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithCustomPageSize_ForwardsToSnapshotRepo()
    {
        // Arrange
        SetupHappyPath();
        const int pageSize = 100;

        // Act
        await _sut.Handle(
            new GetPriceChangeHistoryQuery(MarketId, ProductId, PageSize: pageSize), default);

        // Assert
        await _snapshotRepo.Received(1)
            .GetPageAsync(
                Arg.Any<Guid>(),
                Arg.Any<string?>(),
                pageSize,
                Arg.Any<DateTime?>(),
                Arg.Any<DateTime?>(),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithDateRange_ForwardsFromAndToToSnapshotRepo()
    {
        // Arrange
        SetupHappyPath();
        var from = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 6, 13, 0, 0, 0, DateTimeKind.Utc);

        // Act
        await _sut.Handle(
            new GetPriceChangeHistoryQuery(MarketId, ProductId, From: from, To: to), default);

        // Assert
        await _snapshotRepo.Received(1)
            .GetPageAsync(
                Arg.Any<Guid>(),
                Arg.Any<string?>(),
                Arg.Any<int>(),
                from,
                to,
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NextCursorFromRepo_IncludedInResult()
    {
        // Arrange
        var mp = SetupHappyPath();
        const string expectedCursor = "some-base64-cursor";
        _snapshotRepo
            .GetPageAsync(
                mp.Id,
                Arg.Any<string?>(),
                Arg.Any<int>(),
                Arg.Any<DateTime?>(),
                Arg.Any<DateTime?>(),
                Arg.Any<CancellationToken>())
            .Returns((BuildSnapshots(mp.Id, 1), expectedCursor));

        // Act
        var result = await _sut.Handle(
            new GetPriceChangeHistoryQuery(MarketId, ProductId), default);

        // Assert
        result.Value.NextCursor.Should().Be(expectedCursor);
    }

    [Fact]
    public async Task Handle_PageSizePreservedInResult()
    {
        // Arrange
        SetupHappyPath();
        const int pageSize = 75;

        // Act
        var result = await _sut.Handle(
            new GetPriceChangeHistoryQuery(MarketId, ProductId, PageSize: pageSize), default);

        // Assert
        result.Value.PageSize.Should().Be(pageSize);
    }

    [Fact]
    public async Task Handle_ItemsMappedCorrectlyFromSnapshots()
    {
        // Arrange
        var mp = SetupHappyPath();

        var actorId = Guid.NewGuid();
        var snapshot = new PriceSnapshot(mp.Id, 120_000m, 80, actorId);
        var capturedRecordedAt = snapshot.RecordedAt;

        _snapshotRepo
            .GetPageAsync(
                mp.Id,
                Arg.Any<string?>(),
                Arg.Any<int>(),
                Arg.Any<DateTime?>(),
                Arg.Any<DateTime?>(),
                Arg.Any<CancellationToken>())
            .Returns((new[] { snapshot } as IReadOnlyList<PriceSnapshot>, (string?)null));

        // Act
        var result = await _sut.Handle(
            new GetPriceChangeHistoryQuery(MarketId, ProductId), default);

        // Assert — every field from PriceSnapshot must map to PriceHistoryItemDto
        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(1);
        var item = result.Value.Items[0];
        item.Id.Should().Be(snapshot.Id);
        item.MarketProductId.Should().Be(mp.Id);
        item.Price.Should().Be(120_000m);
        item.Quantity.Should().Be(80);
        item.RecordedBy.Should().Be(actorId);
        item.RecordedAt.Should().Be(capturedRecordedAt);
    }

    [Fact]
    public async Task Handle_NullRecordedBy_MappedToNull()
    {
        // Arrange — system-initiated update has no actor
        var mp = SetupHappyPath();
        var snapshot = new PriceSnapshot(mp.Id, 100_000m, 50, null);
        _snapshotRepo
            .GetPageAsync(
                mp.Id,
                Arg.Any<string?>(),
                Arg.Any<int>(),
                Arg.Any<DateTime?>(),
                Arg.Any<DateTime?>(),
                Arg.Any<CancellationToken>())
            .Returns((new[] { snapshot } as IReadOnlyList<PriceSnapshot>, (string?)null));

        // Act
        var result = await _sut.Handle(
            new GetPriceChangeHistoryQuery(MarketId, ProductId), default);

        // Assert
        result.Value.Items[0].RecordedBy.Should().BeNull();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Sets up market exists + product listed at market.
    /// Returns the created MarketProduct so tests can reference its auto-generated Id.
    /// </summary>
    private MarketProduct SetupHappyPath()
    {
        _marketReader.MarketExistsAsync(MarketId, default).Returns(true);
        var mp = new MarketProduct(MarketId, ProductId, 100_000m, 50, null);
        _marketProductRepo
            .FindByMarketAndProductAsync(MarketId, ProductId, default)
            .Returns(mp);
        return mp;
    }

    private static IReadOnlyList<PriceSnapshot> BuildSnapshots(Guid marketProductId, int count) =>
        Enumerable.Range(0, count)
            .Select(_ => new PriceSnapshot(marketProductId, 100_000m, 50, null))
            .ToList()
            .AsReadOnly();
}
