using FluentAssertions;
using FreshFlow.Auth.Application.Abstractions;
using FreshFlow.Auth.Application.Commands.Admin.CreateUser;
using FreshFlow.Auth.Domain.Entities;
using NSubstitute;

namespace FreshFlow.Auth.UnitTests.Commands;

[Trait("Category", "Unit")]
public sealed class CreateUserCommandHandlerTests
{
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IRoleRepository _roles = Substitute.For<IRoleRepository>();
    private readonly IPasswordHasher _hasher = Substitute.For<IPasswordHasher>();
    private readonly IRestaurantRepository _restaurants = Substitute.For<IRestaurantRepository>();
    private readonly IDriverProfileCreator _driverCreator = Substitute.For<IDriverProfileCreator>();
    private readonly IMarketValidator _marketValidator = Substitute.For<IMarketValidator>();
    private readonly IUserMarketAssignmentRepository _marketAssignments = Substitute.For<IUserMarketAssignmentRepository>();
    private readonly CreateUserCommandHandler _sut;

    public CreateUserCommandHandlerTests()
    {
        _hasher.Hash(Arg.Any<string>()).Returns("hashed");
        _sut = new CreateUserCommandHandler(
            _users, _roles, _hasher, _restaurants, _driverCreator, _marketValidator, _marketAssignments);
    }

    [Theory]
    [InlineData("hub_staff")]
    [InlineData("driver")]
    public async Task Handle_ValidRole_CreatesUser(string roleName)
    {
        _users.ExistsAsync(Arg.Any<string>(), default).Returns(false);
        _roles.FindByNameAsync(roleName, default).Returns(new Role(roleName, "Some role"));

        var cmd = new CreateUserCommand("u@test.com", "P@ss1", roleName, null, null);
        var result = await _sut.Handle(cmd, default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Email.Should().Be("u@test.com");
        result.Value.Role.Should().Be(roleName);
    }

    [Fact]
    public async Task Handle_DuplicateEmail_ReturnsConflict()
    {
        _users.ExistsAsync("dup@test.com", default).Returns(true);

        var result = await _sut.Handle(
            new CreateUserCommand("dup@test.com", "P@ss1", "driver", null, null), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("EMAIL_ALREADY_EXISTS");
    }

    [Fact]
    public async Task Handle_DuplicatePhone_ReturnsConflict()
    {
        const string phone = "+84901234567";
        _users.ExistsAsync(Arg.Any<string>(), default).Returns(false);
        _users.ExistsByPhoneAsync(phone, default).Returns(true);

        var result = await _sut.Handle(
            new CreateUserCommand("u@test.com", "P@ss1", "driver", null, null, phone), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("PHONE_ALREADY_EXISTS");
    }

    [Fact]
    public async Task Handle_InvalidRole_ReturnsValidationError()
    {
        _users.ExistsAsync(Arg.Any<string>(), default).Returns(false);
        _roles.FindByNameAsync("unknown_role", default).Returns((Role?)null);

        var result = await _sut.Handle(
            new CreateUserCommand("u@test.com", "P@ss1", "unknown_role", null, null), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("VALIDATION_ERROR");
    }

    [Fact]
    public async Task Handle_InvalidMarketId_ReturnsInvalidMarket()
    {
        var marketId = Guid.NewGuid();
        _users.ExistsAsync(Arg.Any<string>(), default).Returns(false);
        _roles.FindByNameAsync("market_agent", default).Returns(new Role("market_agent", "Market Agent"));
        _marketValidator.IsActiveMarketAsync(marketId, default).Returns(false);

        var result = await _sut.Handle(
            new CreateUserCommand("u@test.com", "P@ss1", "market_agent", marketId, null), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("INVALID_MARKET");
    }

    [Fact]
    public async Task Handle_RestaurantRole_CreatesRestaurantProfile()
    {
        _users.ExistsAsync(Arg.Any<string>(), default).Returns(false);
        _roles.FindByNameAsync("restaurant", default).Returns(new Role("restaurant", "Restaurant"));
        _restaurants.CreateAsync(Arg.Any<Guid>(), "Pho Ba Tu", null, default).Returns(Guid.NewGuid());

        var result = await _sut.Handle(
            new CreateUserCommand("r@test.com", "P@ss1", "restaurant", null, "Pho Ba Tu"), default);

        result.IsSuccess.Should().BeTrue();
        await _restaurants.Received(1).CreateAsync(Arg.Any<Guid>(), "Pho Ba Tu", null, default);
    }

    [Fact]
    public async Task Handle_KioskStaffAlias_NormalizedToMarketAgent()
    {
        var marketId = Guid.NewGuid();
        _users.ExistsAsync(Arg.Any<string>(), default).Returns(false);
        _roles.FindByNameAsync("market_agent", default).Returns(new Role("market_agent", "Market Agent"));
        _marketValidator.IsActiveMarketAsync(marketId, default).Returns(true);

        var result = await _sut.Handle(
            new CreateUserCommand("k@test.com", "P@ss1", "kiosk_staff", marketId, null), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Role.Should().Be("market_agent");
    }

    [Fact]
    public async Task Handle_WithPhone_PhoneStoredOnUser()
    {
        const string phone = "+84901234567";
        _users.ExistsAsync(Arg.Any<string>(), default).Returns(false);
        _users.ExistsByPhoneAsync(phone, default).Returns(false);
        _roles.FindByNameAsync("driver", default).Returns(new Role("driver", "Driver"));

        var result = await _sut.Handle(
            new CreateUserCommand("d@test.com", "P@ss1", "driver", null, null, phone), default);

        result.IsSuccess.Should().BeTrue();
    }

    [Theory]
    [InlineData("  Nguyen Van A  ", "Nguyen Van A")]
    [InlineData("   ", null)]
    public async Task Handle_WithFullName_NormalizesStoresAndReturnsIt(string fullName, string? expected)
    {
        _users.ExistsAsync(Arg.Any<string>(), default).Returns(false);
        _roles.FindByNameAsync("driver", default).Returns(new Role("driver", "Driver"));

        var result = await _sut.Handle(
            new CreateUserCommand("named@test.com", "P@ss1", "driver", null, null,
                FullName: fullName), default);

        result.Value.FullName.Should().Be(expected);
        await _users.Received(1).AddAsync(
            Arg.Is<FreshFlow.Auth.Domain.Aggregates.User>(u => u.FullName == expected), default);
    }

    [Fact]
    public async Task Handle_MarketAgentWithMarketId_CreatesMarketAssignment()
    {
        // Arrange
        var marketId = Guid.NewGuid();
        _users.ExistsAsync(Arg.Any<string>(), default).Returns(false);
        _roles.FindByNameAsync("market_agent", default).Returns(new Role("market_agent", "Market Agent"));
        _marketValidator.IsActiveMarketAsync(marketId, default).Returns(true);

        // Act
        var result = await _sut.Handle(
            new CreateUserCommand("agent@test.com", "P@ss1", "market_agent", marketId, null), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _marketAssignments.Received(1)
            .AddAsync(
                Arg.Is<FreshFlow.Auth.Domain.Entities.UserMarketAssignment>(a =>
                    a.MarketId == marketId && a.AssignedBy == null),
                default);
    }

    [Fact]
    public async Task Handle_NonMarketAgentRole_DoesNotCreateMarketAssignment()
    {
        // Arrange
        _users.ExistsAsync(Arg.Any<string>(), default).Returns(false);
        _roles.FindByNameAsync("driver", default).Returns(new Role("driver", "Driver"));

        // Act
        var result = await _sut.Handle(
            new CreateUserCommand("d@test.com", "P@ss1", "driver", null, null), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _marketAssignments.DidNotReceive()
            .AddAsync(Arg.Any<FreshFlow.Auth.Domain.Entities.UserMarketAssignment>(), default);
    }
}
