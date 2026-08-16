using FluentAssertions;
using FreshFlow.Orders.Application.Abstractions;
using FreshFlow.Orders.Application.Dtos;
using FreshFlow.Orders.Application.Queries.GetFavorites;
using NSubstitute;
using NSubstitute.ReturnsExtensions;

namespace FreshFlow.Orders.UnitTests.Queries;

[Trait("Category", "Unit")]
public sealed class GetFavoritesQueryHandlerTests
{
    private readonly IFavoriteReader _favoriteReader = Substitute.For<IFavoriteReader>();
    private readonly IRestaurantReader _restaurantReader = Substitute.For<IRestaurantReader>();
    private readonly GetFavoritesQueryHandler _sut;

    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid RestaurantId = Guid.NewGuid();

    public GetFavoritesQueryHandlerTests()
    {
        _restaurantReader.FindByUserIdAsync(UserId, Arg.Any<CancellationToken>())
            .Returns(new RestaurantSnapshotDto(RestaurantId, IsApproved: true));
        _sut = new GetFavoritesQueryHandler(_favoriteReader, _restaurantReader);
    }

    [Fact]
    public async Task Handle_RestaurantMissing_ReturnsForbiddenAsync()
    {
        _restaurantReader.FindByUserIdAsync(UserId, Arg.Any<CancellationToken>()).ReturnsNull();

        var result = await _sut.Handle(new GetFavoritesQuery(UserId), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("FORBIDDEN");
    }

    [Fact]
    public async Task Handle_ValidQuery_ReturnsReaderItemsForOwnRestaurantAsync()
    {
        var item = new FavoriteItemDto(
            Guid.NewGuid(), Guid.NewGuid(), "Tomato", null,
            Guid.NewGuid(), "Ben Thanh", "Vegetable", "kg", 10_000m, 42, DateTime.UtcNow);
        _favoriteReader.ListAsync(RestaurantId, Arg.Any<CancellationToken>())
            .Returns(new[] { item });

        var result = await _sut.Handle(new GetFavoritesQuery(UserId), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle().Which.Should().Be(item);
        await _favoriteReader.Received(1).ListAsync(RestaurantId, Arg.Any<CancellationToken>());
    }
}
