using FluentAssertions;
using FreshFlow.Orders.Application.Abstractions;
using FreshFlow.Orders.Application.Commands.Favorites.Remove;
using NSubstitute;
using NSubstitute.ReturnsExtensions;

namespace FreshFlow.Orders.UnitTests.Commands;

[Trait("Category", "Unit")]
public sealed class RemoveFavoriteCommandHandlerTests
{
    private readonly IFavoriteRepository _favorites = Substitute.For<IFavoriteRepository>();
    private readonly IRestaurantReader _restaurantReader = Substitute.For<IRestaurantReader>();
    private readonly RemoveFavoriteCommandHandler _sut;

    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid RestaurantId = Guid.NewGuid();
    private static readonly Guid MarketProductId = Guid.NewGuid();

    public RemoveFavoriteCommandHandlerTests()
    {
        _restaurantReader.FindByUserIdAsync(UserId, Arg.Any<CancellationToken>())
            .Returns(new RestaurantSnapshotDto(RestaurantId, IsApproved: true));
        _sut = new RemoveFavoriteCommandHandler(_favorites, _restaurantReader);
    }

    [Fact]
    public async Task Handle_RestaurantMissing_ReturnsForbiddenAsync()
    {
        _restaurantReader.FindByUserIdAsync(UserId, Arg.Any<CancellationToken>()).ReturnsNull();

        var result = await _sut.Handle(new RemoveFavoriteCommand(UserId, MarketProductId), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("FORBIDDEN");
        await _favorites.DidNotReceive().RemoveAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ValidCommand_RemovesFavoriteAsync()
    {
        var result = await _sut.Handle(new RemoveFavoriteCommand(UserId, MarketProductId), default);

        result.IsSuccess.Should().BeTrue();
        await _favorites.Received(1).RemoveAsync(RestaurantId, MarketProductId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NotFavorited_StillReturnsSuccessAsync()
    {
        // RemoveAsync returning false means there was nothing to delete — un-favoriting twice
        // is not an error (idempotent remove).
        _favorites.RemoveAsync(RestaurantId, MarketProductId, Arg.Any<CancellationToken>())
            .Returns(false);

        var result = await _sut.Handle(new RemoveFavoriteCommand(UserId, MarketProductId), default);

        result.IsSuccess.Should().BeTrue();
    }
}
