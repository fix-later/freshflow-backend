using FluentAssertions;
using FreshFlow.Auth.Application.Abstractions;
using FreshFlow.Auth.Application.Commands.RegisterRestaurant;
using FreshFlow.Auth.Domain.Entities;
using NSubstitute;

namespace FreshFlow.Auth.UnitTests.Commands;

[Trait("Category", "Unit")]
public sealed class RegisterRestaurantCommandHandlerTests
{
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IRoleRepository _roles = Substitute.For<IRoleRepository>();
    private readonly IPasswordHasher _hasher = Substitute.For<IPasswordHasher>();
    private readonly IRestaurantRepository _restaurants = Substitute.For<IRestaurantRepository>();
    private readonly RegisterRestaurantCommandHandler _sut;

    private static readonly Role RestaurantRole = new("restaurant", "Restaurant");

    public RegisterRestaurantCommandHandlerTests()
    {
        _hasher.Hash(Arg.Any<string>()).Returns("hashed");
        _roles.FindByNameAsync("restaurant", default).Returns(RestaurantRole);
        _sut = new RegisterRestaurantCommandHandler(_users, _roles, _hasher, _restaurants);
    }

    [Fact]
    public async Task Handle_DuplicateEmail_ReturnsEmailAlreadyExists()
    {
        // Arrange
        _users.ExistsAsync("owner@phobaatu.vn", default).Returns(true);

        var cmd = new RegisterRestaurantCommand(
            "owner@phobaatu.vn", "MySecureP@ss1", "Phở Bà Tú", "+84901234567");

        // Act
        var result = await _sut.Handle(cmd, default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("EMAIL_ALREADY_EXISTS");
        await _users.DidNotReceive().AddAsync(Arg.Any<FreshFlow.Auth.Domain.Aggregates.User>(), default);
    }

    [Fact]
    public async Task Handle_HappyPath_CreatesUserAndRestaurantWithIsApprovedFalse()
    {
        // Arrange
        var restaurantId = Guid.NewGuid();
        _users.ExistsAsync("owner@phobaatu.vn", default).Returns(false);
        _restaurants.CreateAsync(Arg.Any<Guid>(), "Phở Bà Tú", Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), default).Returns(restaurantId);

        var cmd = new RegisterRestaurantCommand(
            "owner@phobaatu.vn", "MySecureP@ss1", "Phở Bà Tú", "+84901234567");

        // Act
        var result = await _sut.Handle(cmd, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Email.Should().Be("owner@phobaatu.vn");
        result.Value.RestaurantName.Should().Be("Phở Bà Tú");
        result.Value.IsApproved.Should().BeFalse();
        result.Value.UserId.Should().NotBeEmpty();
        result.Value.RestaurantId.Should().Be(restaurantId);

        await _users.Received(1).AddAsync(Arg.Any<FreshFlow.Auth.Domain.Aggregates.User>(), default);
        // SaveChangesAsync is NOT called on users — CreateAsync commits both atomically.
        await _users.DidNotReceive().SaveChangesAsync(default);
        await _restaurants.Received(1).CreateAsync(Arg.Any<Guid>(), "Phở Bà Tú", Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), default);
    }

    [Fact]
    public async Task Handle_HappyPath_WithoutPhone_Succeeds()
    {
        // Arrange
        var restaurantId = Guid.NewGuid();
        _users.ExistsAsync("owner@test.vn", default).Returns(false);
        _restaurants.CreateAsync(Arg.Any<Guid>(), "Test Restaurant", Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), default).Returns(restaurantId);

        var cmd = new RegisterRestaurantCommand("owner@test.vn", "MySecureP@ss1", "Test Restaurant", null);

        // Act
        var result = await _sut.Handle(cmd, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.IsApproved.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WithInvoiceFields_ForwardsTrimmedValuesToRepository()
    {
        // Arrange
        _users.ExistsAsync("owner@test.vn", default).Returns(false);
        _restaurants.CreateAsync(Arg.Any<Guid>(), "Test Restaurant", Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), default)
            .Returns(Guid.NewGuid());

        var cmd = new RegisterRestaurantCommand(
            "owner@test.vn", "MySecureP@ss1", "Test Restaurant", null,
            "  0312345678  ", "  Công ty FreshFlow  ",
            "  123 Nguyễn Huệ, Quận 1  ");

        // Act
        var result = await _sut.Handle(cmd, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _restaurants.Received(1)
            .CreateAsync(Arg.Any<Guid>(), "Test Restaurant", "0312345678",
                "Công ty FreshFlow", "123 Nguyễn Huệ, Quận 1", default);
    }

    [Fact]
    public async Task Handle_BlankTaxCode_ForwardsNullToRepository()
    {
        // Arrange
        _users.ExistsAsync("owner@test.vn", default).Returns(false);
        _restaurants.CreateAsync(Arg.Any<Guid>(), "Test Restaurant", Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), default)
            .Returns(Guid.NewGuid());

        var cmd = new RegisterRestaurantCommand(
            "owner@test.vn", "MySecureP@ss1", "Test Restaurant", null, "   ");

        // Act
        await _sut.Handle(cmd, default);

        // Assert — whitespace-only tax code is normalised to null, not stored as blank.
        await _restaurants.Received(1)
            .CreateAsync(Arg.Any<Guid>(), "Test Restaurant", null, null, null, default);
    }

    [Fact]
    public async Task Handle_DuplicatePhone_ReturnsPhoneAlreadyExists()
    {
        // Arrange
        const string phone = "+84901234567";
        _users.ExistsAsync(Arg.Any<string>(), default).Returns(false);
        _users.ExistsByPhoneAsync(phone, default).Returns(true);

        var cmd = new RegisterRestaurantCommand(
            "owner@phobaatu.vn", "MySecureP@ss1", "Phở Bà Tú", phone);

        // Act
        var result = await _sut.Handle(cmd, default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("PHONE_ALREADY_EXISTS");
        await _users.DidNotReceive().AddAsync(Arg.Any<FreshFlow.Auth.Domain.Aggregates.User>(), default);
    }

    [Fact]
    public async Task Handle_RestaurantCreationFails_PropagatesException()
    {
        // Arrange
        _users.ExistsAsync("owner@test.vn", default).Returns(false);
        _restaurants.CreateAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), default)
            .Returns(Task.FromException<Guid>(new InvalidOperationException("DB error")));

        var cmd = new RegisterRestaurantCommand("owner@test.vn", "MySecureP@ss1", "Test Restaurant", null);

        // Act
        var act = async () => await _sut.Handle(cmd, default);

        // Assert — exception propagates; user was staged but never committed (SaveChangesAsync inside CreateAsync threw)
        await act.Should().ThrowAsync<InvalidOperationException>();
        await _users.Received(1).AddAsync(Arg.Any<FreshFlow.Auth.Domain.Aggregates.User>(), default);
        await _users.DidNotReceive().SaveChangesAsync(default);
    }

    [Fact]
    public async Task Handle_RoleNotFound_ReturnsRoleNotConfigured()
    {
        // Arrange
        _users.ExistsAsync(Arg.Any<string>(), default).Returns(false);
        _roles.FindByNameAsync("restaurant", default).Returns((Role?)null);

        var cmd = new RegisterRestaurantCommand(
            "owner@phobaatu.vn", "MySecureP@ss1", "Phở Bà Tú", null);

        // Act
        var result = await _sut.Handle(cmd, default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ROLE_NOT_CONFIGURED");
    }
}
