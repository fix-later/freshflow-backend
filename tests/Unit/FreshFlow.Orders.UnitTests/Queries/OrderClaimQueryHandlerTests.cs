using FluentAssertions;
using FreshFlow.Orders.Application.Abstractions;
using FreshFlow.Orders.Application.Queries.GetClaimById;
using FreshFlow.Orders.Application.Queries.ListClaims;
using FreshFlow.Orders.Domain.Entities;
using NSubstitute;

namespace FreshFlow.Orders.UnitTests.Queries;

[Trait("Category", "Unit")]
public sealed class OrderClaimQueryHandlerTests
{
    private readonly IOrderClaimRepository _claims = Substitute.For<IOrderClaimRepository>();
    private readonly IRestaurantReader _restaurants = Substitute.For<IRestaurantReader>();

    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid RestaurantId = Guid.NewGuid();
    private static readonly Guid OtherRestaurantId = Guid.NewGuid();

    public OrderClaimQueryHandlerTests()
    {
        _restaurants.FindByUserIdAsync(UserId, Arg.Any<CancellationToken>())
            .Returns(new RestaurantSnapshotDto(RestaurantId, IsApproved: true));
    }

    [Fact]
    public async Task GetClaim_RestaurantCannotReadAnotherRestaurantsClaimAsync()
    {
        var claim = NewClaim(OtherRestaurantId);
        _claims.FindByIdAsync(claim.Id, Arg.Any<CancellationToken>()).Returns(claim);
        var sut = new GetClaimByIdQueryHandler(_claims, _restaurants);

        var result = await sut.Handle(
            new GetClaimByIdQuery(UserId, IsPrivileged: false, claim.Id),
            default);

        result.Error.Code.Should().Be("FORBIDDEN");
    }

    [Fact]
    public async Task GetClaim_PrivilegedUserCanReadAnyClaimWithoutRestaurantLookupAsync()
    {
        var claim = NewClaim(OtherRestaurantId);
        _claims.FindByIdAsync(claim.Id, Arg.Any<CancellationToken>()).Returns(claim);
        var sut = new GetClaimByIdQueryHandler(_claims, _restaurants);

        var result = await sut.Handle(
            new GetClaimByIdQuery(UserId, IsPrivileged: true, claim.Id),
            default);

        result.IsSuccess.Should().BeTrue();
        result.Value.ClaimId.Should().Be(claim.Id);
        await _restaurants.DidNotReceive()
            .FindByUserIdAsync(UserId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ListClaims_RestaurantIsForcedToOwnedRestaurantAsync()
    {
        var claim = NewClaim(RestaurantId);
        _claims.SearchAsync(
                Arg.Any<OrderClaimSearchCriteria>(),
                Arg.Any<CancellationToken>())
            .Returns(([claim], "next"));
        var sut = new ListClaimsQueryHandler(_claims, _restaurants);

        var result = await sut.Handle(
            new ListClaimsQuery(
                UserId,
                IsPrivileged: false,
                RestaurantId: null,
                Status: "submitted",
                Cursor: null,
                PageSize: 20),
            default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().ContainSingle();
        result.Value.NextCursor.Should().Be("next");
        await _claims.Received(1).SearchAsync(
            Arg.Is<OrderClaimSearchCriteria>(criteria =>
                criteria.RestaurantId == RestaurantId
                && criteria.PageSize == 20),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ListClaims_RestaurantFilteringOtherRestaurant_ReturnsForbiddenAsync()
    {
        var sut = new ListClaimsQueryHandler(_claims, _restaurants);

        var result = await sut.Handle(
            new ListClaimsQuery(
                UserId,
                IsPrivileged: false,
                RestaurantId: OtherRestaurantId,
                Status: null,
                Cursor: null,
                PageSize: 20),
            default);

        result.Error.Code.Should().Be("FORBIDDEN");
        await _claims.DidNotReceive().SearchAsync(
            Arg.Any<OrderClaimSearchCriteria>(),
            Arg.Any<CancellationToken>());
    }

    private static OrderClaim NewClaim(Guid restaurantId) =>
        new(
            Guid.NewGuid(),
            restaurantId,
            10_000m,
            "Damaged",
            UserId,
            new DateTime(2026, 8, 4, 1, 0, 0, DateTimeKind.Utc));
}
