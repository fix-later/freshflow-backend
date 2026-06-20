using FluentAssertions;
using FreshFlow.Pricing.Application.Abstractions;
using FreshFlow.Pricing.Application.Queries.SearchMarketProducts;
using NSubstitute;

namespace FreshFlow.Pricing.UnitTests.Queries;

[Trait("Category", "Unit")]
public sealed class SearchMarketProductsQueryHandlerTests
{
    private readonly IMarketProductRepository _repository = Substitute.For<IMarketProductRepository>();
    private readonly SearchMarketProductsQueryHandler _sut;

    private static readonly Guid MarketId = Guid.NewGuid();

    public SearchMarketProductsQueryHandlerTests()
    {
        _sut = new SearchMarketProductsQueryHandler(_repository);
    }

    private static MarketProductSearchItemDto NewItem(string name = "Cà chua") =>
        new(Guid.NewGuid(), Guid.NewGuid(), name, "Vegetables", 20_000m, AvailableQuantity: 50);

    [Fact]
    public async Task Handle_HappyPath_ReturnsItemsAndNextCursorFromRepositoryAsync()
    {
        var items = new[] { NewItem("Cà chua"), NewItem("Cà rốt") };
        _repository.SearchAsync(Arg.Any<MarketProductSearchCriteria>(), Arg.Any<CancellationToken>())
            .Returns((items, "next-cursor-token"));

        var result = await _sut.Handle(
            new SearchMarketProductsQuery(MarketId, "Cà"), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().BeEquivalentTo(items);
        result.Value.NextCursor.Should().Be("next-cursor-token");
    }

    [Fact]
    public async Task Handle_NoMatches_ReturnsEmptyItemsAndNullCursorAsync()
    {
        _repository.SearchAsync(Arg.Any<MarketProductSearchCriteria>(), Arg.Any<CancellationToken>())
            .Returns((Array.Empty<MarketProductSearchItemDto>(), (string?)null));

        var result = await _sut.Handle(
            new SearchMarketProductsQuery(MarketId, "no-such-product"), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().BeEmpty();
        result.Value.NextCursor.Should().BeNull();
    }

    [Fact]
    public async Task Handle_LastPage_ReturnsNullNextCursorAsync()
    {
        var items = new[] { NewItem() };
        _repository.SearchAsync(Arg.Any<MarketProductSearchCriteria>(), Arg.Any<CancellationToken>())
            .Returns((items, (string?)null));

        var result = await _sut.Handle(
            new SearchMarketProductsQuery(MarketId, "Cà", Cursor: "previous-cursor"), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.NextCursor.Should().BeNull();
    }

    [Fact]
    public async Task Handle_MapsQueryFieldsToSearchCriteriaAsync()
    {
        _repository.SearchAsync(Arg.Any<MarketProductSearchCriteria>(), Arg.Any<CancellationToken>())
            .Returns((Array.Empty<MarketProductSearchItemDto>(), (string?)null));

        await _sut.Handle(
            new SearchMarketProductsQuery(
                MarketId, "Cà", Category: "Vegetables", InStockOnly: true, Cursor: "cur", PageSize: 10),
            default);

        await _repository.Received(1).SearchAsync(
            Arg.Is<MarketProductSearchCriteria>(c =>
                c.MarketId == MarketId
                && c.SearchText == "Cà"
                && c.Category == "Vegetables"
                && c.InStockOnly
                && c.Cursor == "cur"
                && c.PageSize == 10),
            Arg.Any<CancellationToken>());
    }
}
