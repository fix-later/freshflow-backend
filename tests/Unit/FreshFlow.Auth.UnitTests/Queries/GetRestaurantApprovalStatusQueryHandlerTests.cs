using FluentAssertions;
using FreshFlow.Auth.Application.Abstractions;
using FreshFlow.Auth.Application.Queries.GetRestaurantApprovalStatus;
using FreshFlow.Auth.Domain.Enums;
using NSubstitute;

namespace FreshFlow.Auth.UnitTests.Queries;

[Trait("Category", "Unit")]
public sealed class GetRestaurantApprovalStatusQueryHandlerTests
{
    private readonly IRestaurantRepository _restaurants = Substitute.For<IRestaurantRepository>();
    private readonly GetRestaurantApprovalStatusQueryHandler _sut;

    public GetRestaurantApprovalStatusQueryHandlerTests() =>
        _sut = new GetRestaurantApprovalStatusQueryHandler(_restaurants);

    private static RestaurantDto Dto(RestaurantStatus status) =>
        new(Guid.NewGuid(), "Test Restaurant", status, DateTime.UtcNow, Guid.NewGuid());

    [Fact]
    public async Task Handle_PendingRestaurant_ReturnsPendingStatus()
    {
        var userId = Guid.NewGuid();
        _restaurants.FindByUserIdAsync(userId, default).Returns(Dto(RestaurantStatus.Pending));

        var result = await _sut.Handle(new GetRestaurantApprovalStatusQuery(userId), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("pending");
    }

    [Fact]
    public async Task Handle_ActiveRestaurant_ReturnsActiveStatus()
    {
        var userId = Guid.NewGuid();
        _restaurants.FindByUserIdAsync(userId, default).Returns(Dto(RestaurantStatus.Active));

        var result = await _sut.Handle(new GetRestaurantApprovalStatusQuery(userId), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("active");
    }

    [Fact]
    public async Task Handle_SuspendedRestaurant_ReturnsSuspendedStatus()
    {
        var userId = Guid.NewGuid();
        _restaurants.FindByUserIdAsync(userId, default).Returns(Dto(RestaurantStatus.Suspended));

        var result = await _sut.Handle(new GetRestaurantApprovalStatusQuery(userId), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("suspended");
    }

    [Fact]
    public async Task Handle_NoRestaurantLinked_ReturnsNotFoundError()
    {
        var userId = Guid.NewGuid();
        _restaurants.FindByUserIdAsync(userId, default).Returns((RestaurantDto?)null);

        var result = await _sut.Handle(new GetRestaurantApprovalStatusQuery(userId), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("RESTAURANT_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_Success_ResponseContainsRestaurantIdAndUpdatedAt()
    {
        var userId = Guid.NewGuid();
        var dto = Dto(RestaurantStatus.Active);
        _restaurants.FindByUserIdAsync(userId, default).Returns(dto);

        var result = await _sut.Handle(new GetRestaurantApprovalStatusQuery(userId), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.RestaurantId.Should().Be(dto.Id);
        result.Value.UpdatedAt.Should().Be(dto.UpdatedAt);
    }
}
