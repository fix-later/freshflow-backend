using FluentAssertions;
using FreshFlow.Auth.Application.Abstractions;
using FreshFlow.Auth.Application.Queries.GetUsers;
using FreshFlow.Auth.Domain.Aggregates;
using FreshFlow.Auth.Domain.Entities;
using FreshFlow.Auth.Domain.Enums;
using NSubstitute;

namespace FreshFlow.Auth.UnitTests.Queries;

[Trait("Category", "Unit")]
public sealed class GetUsersQueryHandlerTests
{
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IRestaurantRepository _restaurants = Substitute.For<IRestaurantRepository>();
    private readonly GetUsersQueryHandler _sut;

    public GetUsersQueryHandlerTests() =>
        _sut = new GetUsersQueryHandler(_users, _restaurants);

    [Fact]
    public async Task Handle_NoFilters_ReturnsPaginatedUsers()
    {
        var users = new List<User> { User.Create("a@test.com", "h", new Role("driver", "Driver"), fullName: "A Driver") };
        _users.GetPagedAsync(null, null, null, 1, 20, null, default).Returns((users, 1));

        var result = await _sut.Handle(new GetUsersQuery(null, null, null), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Data.Should().HaveCount(1);
        result.Value.Data[0].FullName.Should().Be("A Driver");
        result.Value.Meta.Total.Should().Be(1);
    }

    [Fact]
    public async Task Handle_RestaurantUser_IncludesIsApproved()
    {
        var user = User.Create("r@test.com", "h", new Role("restaurant", "Restaurant"));
        var restaurantId = Guid.NewGuid();
        _users.GetPagedAsync(null, null, null, 1, 20, null, default)
            .Returns((new List<User> { user }, 1));
        _restaurants.FindByUserIdAsync(user.Id, default)
            .Returns(new RestaurantDto(restaurantId, "Test", RestaurantStatus.Active, DateTime.UtcNow, user.Id));

        var result = await _sut.Handle(new GetUsersQuery(null, null, null), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Data[0].IsApproved.Should().BeTrue();
        result.Value.Data[0].RestaurantId.Should().Be(restaurantId);
        result.Value.Data[0].RestaurantStatus.Should().Be("active");
        result.Value.Data[0].RestaurantName.Should().Be("Test");
        result.Value.Data[0].FullName.Should().BeNull();
    }

    [Fact]
    public async Task Handle_RestaurantStatusFilter_PassesThroughToRepository()
    {
        _users.GetPagedAsync(null, null, null, 1, 20, "pending", default)
            .Returns((new List<User>(), 0));

        var result = await _sut.Handle(new GetUsersQuery(null, null, null, RestaurantStatus: "pending"), default);

        result.IsSuccess.Should().BeTrue();
        await _users.Received(1).GetPagedAsync(null, null, null, 1, 20, "pending", default);
    }
}
