using FluentAssertions;
using FreshFlow.Auth.Application.Abstractions;
using FreshFlow.Auth.Application.Commands.Admin.ApproveRestaurant;
using NSubstitute;

namespace FreshFlow.Auth.UnitTests.Commands;

[Trait("Category", "Unit")]
public sealed class ApproveRestaurantCommandHandlerTests
{
    private readonly IRestaurantRepository _restaurants = Substitute.For<IRestaurantRepository>();
    private readonly ApproveRestaurantCommandHandler _sut;

    public ApproveRestaurantCommandHandlerTests() =>
        _sut = new ApproveRestaurantCommandHandler(_restaurants);

    [Fact]
    public async Task Handle_PendingRestaurant_ApprovesAndReturnsResponse()
    {
        var id = Guid.NewGuid();
        var dto = new RestaurantDto(id, "Pho Ba Tu", false, DateTime.UtcNow, Guid.NewGuid());
        _restaurants.FindByIdAsync(id, default).Returns(dto);
        _restaurants.ApproveAsync(id, default).Returns(true);

        var result = await _sut.Handle(new ApproveRestaurantCommand(id), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.IsApproved.Should().BeTrue();
        result.Value.RestaurantName.Should().Be("Pho Ba Tu");
    }

    [Fact]
    public async Task Handle_NotFound_ReturnsNotFound()
    {
        _restaurants.FindByIdAsync(Arg.Any<Guid>(), default).Returns((RestaurantDto?)null);

        var result = await _sut.Handle(new ApproveRestaurantCommand(Guid.NewGuid()), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("RESTAURANT_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_AlreadyApproved_ReturnsAlreadyApproved()
    {
        var id = Guid.NewGuid();
        var dto = new RestaurantDto(id, "Test", true, DateTime.UtcNow, Guid.NewGuid());
        _restaurants.FindByIdAsync(id, default).Returns(dto);

        var result = await _sut.Handle(new ApproveRestaurantCommand(id), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ALREADY_APPROVED");
    }
}
