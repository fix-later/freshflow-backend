using FluentAssertions;
using FreshFlow.Auth.Application.Abstractions;
using FreshFlow.Auth.Application.Queries.GetUsers;
using FreshFlow.Auth.Domain.Aggregates;
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
        var users = new List<User> { User.Create("a@test.com", "h", UserRole.Driver) };
        _users.GetPagedAsync(null, null, null, 1, 20, default).Returns((users, 1));

        var result = await _sut.Handle(new GetUsersQuery(null, null, null), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Data.Should().HaveCount(1);
        result.Value.Meta.Total.Should().Be(1);
    }

    [Fact]
    public async Task Handle_RestaurantUser_IncludesIsApproved()
    {
        var user = User.Create("r@test.com", "h", UserRole.Restaurant);
        _users.GetPagedAsync(null, null, null, 1, 20, default)
            .Returns((new List<User> { user }, 1));
        _restaurants.FindByUserIdAsync(user.Id, default)
            .Returns(new RestaurantDto(Guid.NewGuid(), "Test", true, DateTime.UtcNow, user.Id));

        var result = await _sut.Handle(new GetUsersQuery(null, null, null), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Data[0].IsApproved.Should().BeTrue();
    }
}
