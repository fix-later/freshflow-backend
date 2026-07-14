using FluentAssertions;
using FreshFlow.Auth.Application.Abstractions;
using FreshFlow.Auth.Application.Commands.Admin.ReactivateRestaurant;
using FreshFlow.Auth.Domain.Enums;
using NSubstitute;

namespace FreshFlow.Auth.UnitTests.Commands;

[Trait("Category", "Unit")]
public sealed class ReactivateRestaurantCommandHandlerTests
{
    private readonly IRestaurantRepository _restaurants = Substitute.For<IRestaurantRepository>();
    private readonly ReactivateRestaurantCommandHandler _sut;

    public ReactivateRestaurantCommandHandlerTests() =>
        _sut = new ReactivateRestaurantCommandHandler(_restaurants);

    [Fact]
    public async Task Handle_SuspendedRestaurant_ReactivatesAndReturnsResponseAsync()
    {
        var id = Guid.NewGuid();
        var dto = new RestaurantDto(id, "Pho Ba Tu", RestaurantStatus.Suspended, DateTime.UtcNow, Guid.NewGuid());
        _restaurants.FindByIdAsync(id, default).Returns(dto);
        _restaurants.ApproveAsync(id, default).Returns(true);

        var result = await _sut.Handle(new ReactivateRestaurantCommand(id), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.IsActive.Should().BeTrue();
        result.Value.RestaurantName.Should().Be("Pho Ba Tu");
        await _restaurants.Received(1).ApproveAsync(id, default);
    }

    [Fact]
    public async Task Handle_NotFound_ReturnsNotFoundAsync()
    {
        _restaurants.FindByIdAsync(Arg.Any<Guid>(), default).Returns((RestaurantDto?)null);

        var result = await _sut.Handle(new ReactivateRestaurantCommand(Guid.NewGuid()), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("RESTAURANT_NOT_FOUND");
    }

    [Theory]
    [InlineData(RestaurantStatus.Pending)]
    [InlineData(RestaurantStatus.Active)]
    public async Task Handle_NotSuspended_ReturnsNotSuspendedAndDoesNotApproveAsync(RestaurantStatus status)
    {
        var id = Guid.NewGuid();
        var dto = new RestaurantDto(id, "Test", status, DateTime.UtcNow, Guid.NewGuid());
        _restaurants.FindByIdAsync(id, default).Returns(dto);

        var result = await _sut.Handle(new ReactivateRestaurantCommand(id), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("NOT_SUSPENDED");
        await _restaurants.DidNotReceive().ApproveAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }
}
