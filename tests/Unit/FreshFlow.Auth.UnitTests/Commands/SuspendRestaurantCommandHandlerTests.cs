using FluentAssertions;
using FreshFlow.Auth.Application.Abstractions;
using FreshFlow.Auth.Application.Commands.Admin.SuspendRestaurant;
using FreshFlow.Auth.Domain.Enums;
using NSubstitute;

namespace FreshFlow.Auth.UnitTests.Commands;

[Trait("Category", "Unit")]
public sealed class SuspendRestaurantCommandHandlerTests
{
    private readonly IRestaurantRepository _restaurants = Substitute.For<IRestaurantRepository>();
    private readonly SuspendRestaurantCommandHandler _sut;

    public SuspendRestaurantCommandHandlerTests() =>
        _sut = new SuspendRestaurantCommandHandler(_restaurants);

    [Fact]
    public async Task Handle_ActiveRestaurant_SuspendsAndReturnsResponse()
    {
        var id = Guid.NewGuid();
        var dto = new RestaurantDto(id, "Pho Ba Tu", RestaurantStatus.Active, DateTime.UtcNow, Guid.NewGuid());
        _restaurants.FindByIdAsync(id, default).Returns(dto);
        _restaurants.SuspendAsync(id, default).Returns(true);

        var result = await _sut.Handle(new SuspendRestaurantCommand(id), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.IsSuspended.Should().BeTrue();
        result.Value.RestaurantName.Should().Be("Pho Ba Tu");
        await _restaurants.Received(1).SuspendAsync(id, default);
    }

    [Fact]
    public async Task Handle_NotFound_ReturnsNotFound()
    {
        _restaurants.FindByIdAsync(Arg.Any<Guid>(), default).Returns((RestaurantDto?)null);

        var result = await _sut.Handle(new SuspendRestaurantCommand(Guid.NewGuid()), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("RESTAURANT_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_AlreadySuspended_ReturnsAlreadySuspended()
    {
        var id = Guid.NewGuid();
        var dto = new RestaurantDto(id, "Test", RestaurantStatus.Suspended, DateTime.UtcNow, Guid.NewGuid());
        _restaurants.FindByIdAsync(id, default).Returns(dto);

        var result = await _sut.Handle(new SuspendRestaurantCommand(id), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ALREADY_SUSPENDED");
    }

    [Fact]
    public async Task Handle_PendingRestaurant_ReturnsValidationError()
    {
        var id = Guid.NewGuid();
        var dto = new RestaurantDto(id, "Test", RestaurantStatus.Pending, DateTime.UtcNow, Guid.NewGuid());
        _restaurants.FindByIdAsync(id, default).Returns(dto);

        var result = await _sut.Handle(new SuspendRestaurantCommand(id), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("NOT_ACTIVE");
    }
}
