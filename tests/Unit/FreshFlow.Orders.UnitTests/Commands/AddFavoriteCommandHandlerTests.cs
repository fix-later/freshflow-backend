using FluentAssertions;
using FreshFlow.Orders.Application.Abstractions;
using FreshFlow.Orders.Application.Commands.Favorites.Add;
using FreshFlow.Orders.Domain.Entities;
using NSubstitute;
using NSubstitute.ReturnsExtensions;

namespace FreshFlow.Orders.UnitTests.Commands;

[Trait("Category", "Unit")]
public sealed class AddFavoriteCommandHandlerTests
{
    private readonly IFavoriteRepository _favorites = Substitute.For<IFavoriteRepository>();
    private readonly IRestaurantReader _restaurantReader = Substitute.For<IRestaurantReader>();
    private readonly IMarketProductReader _marketProductReader = Substitute.For<IMarketProductReader>();
    private readonly AddFavoriteCommandHandler _sut;

    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid RestaurantId = Guid.NewGuid();
    private static readonly Guid MarketProductId = Guid.NewGuid();

    public AddFavoriteCommandHandlerTests()
    {
        _restaurantReader.FindByUserIdAsync(UserId, Arg.Any<CancellationToken>())
            .Returns(new RestaurantSnapshotDto(RestaurantId, IsApproved: true));
        _marketProductReader.FindAsync(MarketProductId, Arg.Any<CancellationToken>())
            .Returns(new MarketProductSnapshotDto(MarketProductId, "Tomato", 10_000m, 50));
        _sut = new AddFavoriteCommandHandler(_favorites, _restaurantReader, _marketProductReader);
    }

    [Fact]
    public async Task Handle_RestaurantMissing_ReturnsForbiddenAsync()
    {
        _restaurantReader.FindByUserIdAsync(UserId, Arg.Any<CancellationToken>()).ReturnsNull();

        var result = await _sut.Handle(new AddFavoriteCommand(UserId, MarketProductId), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("FORBIDDEN");
        await _favorites.DidNotReceive().AddAsync(Arg.Any<RestaurantFavorite>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_MarketProductMissing_ReturnsNotFoundAsync()
    {
        _marketProductReader.FindAsync(MarketProductId, Arg.Any<CancellationToken>()).ReturnsNull();

        var result = await _sut.Handle(new AddFavoriteCommand(UserId, MarketProductId), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("MARKET_PRODUCT_NOT_FOUND");
        await _favorites.DidNotReceive().AddAsync(Arg.Any<RestaurantFavorite>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ValidCommand_AddsFavoriteAsync()
    {
        var result = await _sut.Handle(new AddFavoriteCommand(UserId, MarketProductId), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.MarketProductId.Should().Be(MarketProductId);
        await _favorites.Received(1).AddAsync(
            Arg.Is<RestaurantFavorite>(f =>
                f.RestaurantId == RestaurantId && f.MarketProductId == MarketProductId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_AlreadyFavorited_StillReturnsSuccessAsync()
    {
        // AddAsync returning false means the repository swallowed a unique-index conflict —
        // the handler treats that as success anyway (idempotent add).
        _favorites.AddAsync(Arg.Any<RestaurantFavorite>(), Arg.Any<CancellationToken>())
            .Returns(false);

        var result = await _sut.Handle(new AddFavoriteCommand(UserId, MarketProductId), default);

        result.IsSuccess.Should().BeTrue();
    }
}
