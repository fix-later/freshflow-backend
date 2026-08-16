using FluentAssertions;
using FreshFlow.Auth.Application.Abstractions;
using FreshFlow.Auth.Application.Queries.GetMyProfile;
using FreshFlow.Auth.Domain.Aggregates;
using FreshFlow.Auth.Domain.Entities;
using NSubstitute;

namespace FreshFlow.Auth.UnitTests.Queries;

[Trait("Category", "Unit")]
public sealed class GetMyProfileQueryHandlerTests
{
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly GetMyProfileQueryHandler _sut;

    public GetMyProfileQueryHandlerTests() =>
        _sut = new GetMyProfileQueryHandler(_users);

    private static User RestaurantUser(string? phone = "+84901234567") =>
        User.Create("owner@pho.vn", "hashed", new Role("restaurant", "Restaurant"), phone);

    [Fact]
    public async Task Handle_ValidQuery_ReturnsProfileWithAllFields()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = RestaurantUser();
        user.UpdateProfile("Nguyen Van A", "+84901234567", "https://cdn.example.com/avatar.jpg");
        _users.FindByIdAsync(userId, default).Returns(user);

        // Act
        var result = await _sut.Handle(new GetMyProfileQuery(userId), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Email.Should().Be("owner@pho.vn");
        result.Value.FullName.Should().Be("Nguyen Van A");
        result.Value.AvatarUrl.Should().Be("https://cdn.example.com/avatar.jpg");
        result.Value.Phone.Should().Be("+84901234567");
        result.Value.Role.Should().Be("restaurant");
    }

    [Fact]
    public async Task Handle_UserNotFound_ReturnsNotFoundError()
    {
        // Arrange
        _users.FindByIdAsync(Arg.Any<Guid>(), default).Returns((User?)null);

        // Act
        var result = await _sut.Handle(new GetMyProfileQuery(Guid.NewGuid()), default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("USER_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_UserWithNullOptionals_ReturnsNullFields()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = RestaurantUser(phone: null);
        _users.FindByIdAsync(userId, default).Returns(user);

        // Act
        var result = await _sut.Handle(new GetMyProfileQuery(userId), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.FullName.Should().BeNull();
        result.Value.AvatarUrl.Should().BeNull();
        result.Value.Phone.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ValidQuery_IdMatchesUser()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = RestaurantUser();
        _users.FindByIdAsync(userId, default).Returns(user);

        // Act
        var result = await _sut.Handle(new GetMyProfileQuery(userId), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(user.Id);
    }
}
