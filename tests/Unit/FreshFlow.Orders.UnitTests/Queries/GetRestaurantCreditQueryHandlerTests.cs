using FluentAssertions;
using FreshFlow.Orders.Application.Abstractions;
using FreshFlow.Orders.Application.Queries.GetRestaurantCredit;
using FreshFlow.Orders.Domain.Entities;
using NSubstitute;

namespace FreshFlow.Orders.UnitTests.Queries;

[Trait("Category", "Unit")]
public sealed class GetRestaurantCreditQueryHandlerTests
{
    private readonly ICreditRepository _creditRepository = Substitute.For<ICreditRepository>();
    private readonly IRestaurantReader _restaurantReader = Substitute.For<IRestaurantReader>();
    private readonly GetRestaurantCreditQueryHandler _sut;

    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid RestaurantId = Guid.NewGuid();
    private static readonly Guid OtherRestaurantId = Guid.NewGuid();

    public GetRestaurantCreditQueryHandlerTests()
    {
        _restaurantReader.FindByIdAsync(RestaurantId, default)
            .Returns(new RestaurantSnapshotDto(RestaurantId, IsApproved: true));
        _restaurantReader.FindByUserIdAsync(UserId, default)
            .Returns(new RestaurantSnapshotDto(RestaurantId, IsApproved: true));
        _sut = new GetRestaurantCreditQueryHandler(_creditRepository, _restaurantReader);
    }

    [Fact]
    public async Task Handle_RestaurantMissing_ReturnsNotFoundAsync()
    {
        _restaurantReader.FindByIdAsync(RestaurantId, default)
            .Returns((RestaurantSnapshotDto?)null);

        var result = await _sut.Handle(new GetRestaurantCreditQuery(UserId, IsAdmin: false, RestaurantId), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("RESTAURANT_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_RestaurantUserForDifferentRestaurant_ReturnsForbiddenAsync()
    {
        _restaurantReader.FindByIdAsync(OtherRestaurantId, default)
            .Returns(new RestaurantSnapshotDto(OtherRestaurantId, IsApproved: true));

        var result = await _sut.Handle(new GetRestaurantCreditQuery(UserId, IsAdmin: false, OtherRestaurantId), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("FORBIDDEN");
    }

    [Fact]
    public async Task Handle_AdminCanReadAnyRestaurantCreditAsync()
    {
        var account = new RestaurantCredit(RestaurantId, creditLimit: 1_000m);
        account.Charge(250m);
        _creditRepository.FindAccountAsync(RestaurantId, default).Returns(account);

        var result = await _sut.Handle(new GetRestaurantCreditQuery(UserId, IsAdmin: true, RestaurantId), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.CreditLimit.Should().Be(1_000m);
        result.Value.OutstandingBalance.Should().Be(250m);
        result.Value.AvailableCredit.Should().Be(750m);
        await _restaurantReader.DidNotReceive().FindByUserIdAsync(UserId, default);
    }

    [Fact]
    public async Task Handle_MissingCreditAccount_ReturnsZeroLimitSnapshotWithoutPersistingAsync()
    {
        _creditRepository.FindAccountAsync(RestaurantId, default)
            .Returns((RestaurantCredit?)null);

        var result = await _sut.Handle(new GetRestaurantCreditQuery(UserId, IsAdmin: false, RestaurantId), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.CreditLimit.Should().Be(0m);
        result.Value.OutstandingBalance.Should().Be(0m);
        result.Value.AvailableCredit.Should().Be(0m);
        await _creditRepository.DidNotReceive().AddAccountAsync(Arg.Any<RestaurantCredit>(), default);
    }
}
